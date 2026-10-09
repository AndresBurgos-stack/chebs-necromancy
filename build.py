#!/usr/bin/env python3
"""Chebs Necromancy Enhanced - Windows build suite.

Uso:
    py build.py                        Compila Release (default)
    py build.py --config Debug         Compila Debug
    py build.py --no-pull              Salta el resync con remoto
    py build.py --pr "titulo"          Build + rama + commit + PR (nunca toca master)
    py build.py --pr "titulo" --dry-run  Muestra lo que haria sin hacerlo

Hace: herramientas -> resync git -> detecta Valheim/BepInEx ->
restore+build -> reporte warnings/errores -> ruta del DLL.
Requiere: .NET SDK 8+, git, python3. Para --pr: gh CLI logueado.
Env overrides: VALHEIM_DIR, BEPINEX_DIR.
Exit codes: 0 ok / 1 fallo build / 2 fallo entorno / 3 fallo git/pr.
"""

import argparse
import datetime
import os
import re
import shutil
import subprocess
import sys
import time
import winreg

REPO = os.path.dirname(os.path.abspath(__file__))
SLN = os.path.join(REPO, "ChebsNecromancy.sln")
LOG = os.path.join(REPO, "build.log")
ALLOW_NEW_EXTS = {".cs", ".csproj", ".json", ".md", ".png", ".ps1", ".py", ".bat", ".props", ".targets"}
ALLOW_NEW_DIRS = ("ChebsNecromancy/", "Translations/")
JUNK_PATTERNS = (".rar", ".zip", "/bin/", "/obj/", "\\bin\\", "\\obj\\", ".vs/", ".log", ".user")


def log(msg):
    print(msg, flush=True)


def run(cmd, logfile=None, **kw):
    if logfile is not None:
        return subprocess.run(cmd, cwd=REPO, stdout=logfile, stderr=subprocess.STDOUT,
                              text=True, errors="replace", **kw)
    return subprocess.run(cmd, cwd=REPO, capture_output=True, text=True, errors="replace", **kw)


def fail(msg, code=2):
    log(f"[ERROR] {msg}")
    sys.exit(code)


def step(n, total, msg):
    log(f"=== [{n}/{total}] {msg} ===")


def check_tools():
    for tool in ("dotnet", "git"):
        if shutil.which(tool) is None:
            fail(f"Falta {tool} en el PATH.")
    r = run(["dotnet", "--list-sdks"])
    majors = sorted({int(m.group(1)) for m in re.finditer(r"^(\d+)\.", r.stdout, re.M) if m})
    if not majors or max(majors) < 8:
        fail("Se requiere .NET SDK 8 o superior.")
    log(f"dotnet SDKs: {', '.join(sorted({m.group(0) for m in SDK_RE.finditer(r.stdout)}))}")


def git_info():
    r = run(["git", "branch", "--show-current"])
    log(f"rama: {r.stdout.strip() or '(detached)'}")
    r = run(["git", "status", "--short"])
    dirty = [l for l in r.stdout.splitlines() if l.strip()]
    log(f"archivos modificados: {len(dirty)}")
    for line in dirty[:15]:
        log(f"  {line}")


def git_pull(skip):
    if skip:
        log("resync omitido (--no-pull).")
        return
    r = run(["git", "pull", "--ff-only"])
    if r.returncode != 0:
        log("[AVISO] git pull fallo (conflicto o sin red). Compilo codigo local, sin tocar tu arbol.")
    else:
        log("remoto al dia.")


def steam_path():
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam") as k:
            return winreg.QueryValueEx(k, "SteamPath")[0].replace("/", "\\")
    except OSError:
        return None


def library_folders(steam):
    out = []
    vdf = os.path.join(steam, "steamapps", "libraryfolders.vdf")
    try:
        with open(vdf, encoding="utf-8", errors="replace") as f:
            content = f.read()
    except OSError:
        return out
    for m in re.finditer(r'"path"\s+"([^"]+)"', content):
        out.append(m.group(1).replace("\\\\", "\\"))
    for m in re.finditer(r'"\d+"\s+"([A-Za-z]:\\\\[^"]+)"', content):
        out.append(m.group(1).replace("\\\\", "\\"))
    return out


def find_valheim():
    if os.environ.get("VALHEIM_DIR"):
        return os.environ["VALHEIM_DIR"]
    candidates = []
    steam = steam_path()
    if steam:
        candidates.append(os.path.join(steam, "steamapps", "common", "Valheim"))
        candidates += [os.path.join(lib, "steamapps", "common", "Valheim") for lib in library_folders(steam)]
    candidates += [
        r"C:\Program Files (x86)\Steam\steamapps\common\Valheim",
        r"C:\Program Files\Steam\steamapps\common\Valheim",
        r"D:\SteamLibrary\steamapps\common\Valheim",
        r"C:\SteamLibrary\steamapps\common\Valheim",
        r"E:\SteamLibrary\steamapps\common\Valheim",
    ]
    seen = set()
    for path in candidates:
        if path in seen:
            continue
        seen.add(path)
        if os.path.isfile(os.path.join(path, "valheim.exe")):
            return path
    return None


def find_bepinex(valheim_dir):
    if os.environ.get("BEPINEX_DIR"):
        return os.environ["BEPINEX_DIR"]
    game_bex = os.path.join(valheim_dir, "BepInEx")
    if os.path.isfile(os.path.join(game_bex, "core", "BepInEx.dll")):
        return game_bex
    appdata = os.environ.get("APPDATA", "")
    for profiles in (
        r"Thunderstore Mod Manager\DataFolder\Valheim\profiles",
        r"r2modmanPlus-local\Valheim\profiles",
        r"r2modman\Valheim\profiles",
        os.path.join("com.kesomannen.gale", "valheim", "profiles"),
    ):
        base = os.path.join(appdata, profiles)
        if not os.path.isdir(base):
            continue
        for profile in sorted(os.listdir(base)):
            cand = os.path.join(base, profile, "BepInEx")
            if os.path.isfile(os.path.join(cand, "core", "BepInEx.dll")):
                return cand
    return None


MSBUILD_RE = re.compile(r"^(.*?)\((\d+)(?:,(\d+))?\):\s+(warning|error)\s+(\w+):\s*(.*)$")
SDK_RE = re.compile(r"^\d+\.\d+\.\d+", re.M)


def parse_diagnostics(text):
    warnings, errors = [], []
    for line in text.splitlines():
        m = MSBUILD_RE.match(line.strip())
        if not m:
            continue
        entry = f"{m.group(1)}:{m.group(2)} [{m.group(5)}] {m.group(6)[:160]}"
        (warnings if m.group(4) == "warning" else errors).append(entry)
    return warnings, errors


KNOWN_HARMLESS = ("MSB3245", "CS0219")


def build(config, valheim_dir, bepinex_dir):
    managed = os.path.join(valheim_dir, "valheim_Data", "Managed")
    for need in (os.path.join(managed, "assembly_valheim.dll"),
                 os.path.join(bepinex_dir, "core", "BepInEx.dll")):
        if not os.path.isfile(need):
            fail(f"Falta archivo requerido: {need}")
    log(f"Valheim: {valheim_dir}")
    log(f"BepInEx: {bepinex_dir}")
    t0 = time.time()
    with open(LOG, "w", encoding="utf-8", errors="replace") as lf:
        r = run(["dotnet", "restore", SLN], logfile=lf)
        if r.returncode != 0:
            fail("dotnet restore fallo (revisa NuGet/red). Detalle en build.log.", 1)
        r = run(["dotnet", "build", SLN, "-c", config, "--no-restore",
                 f"-p:VALHEIM_DATA_MANAGED={managed}", f"-p:BEPINEX_PATH={bepinex_dir}"],
                logfile=lf)
    with open(LOG, encoding="utf-8", errors="replace") as lf:
        warnings, errors = parse_diagnostics(lf.read())
    warnings = list(dict.fromkeys(warnings))
    errors = list(dict.fromkeys(errors))
    log(f"---- warnings ({len(warnings)}) ----")
    for w in warnings[:30]:
        tag = " [conocido/inofensivo]" if w.split("[")[1].split("]")[0] in KNOWN_HARMLESS else ""
        log(f"  {w}{tag}")
    log(f"---- errores ({len(errors)}) ----")
    for e in errors:
        log(f"  {e}")
    log(f"tiempo: {time.time() - t0:.1f}s")
    return r.returncode, warnings, errors


def dll_path(config):
    return os.path.join(REPO, "ChebsNecromancy", "bin", config, "net48", "ChebsNecromancy.dll")


def report_dll(config, build_start):
    path = dll_path(config)
    if not os.path.isfile(path):
        fail("No se genero DLL (build fallido).", 1)
    st = os.stat(path)
    fresh = "RECIEN GENERADO" if st.st_mtime >= build_start else "VIEJO (el build no lo actualizo)"
    log(f"[OK] DLL: {path}")
    log(f"[OK] {st.st_size // 1024} KB, {time.ctime(st.st_mtime)} [{fresh}]")
    return path


def git_status_porcelain():
    r = run(["git", "status", "--porcelain=v1", "--untracked-files=all"])
    if r.returncode != 0:
        fail("No es un repo git o git fallo.", 3)
    return r.stdout.splitlines()


def pr_candidates():
    staged, skipped = [], []
    for line in git_status_porcelain():
        if len(line) < 4:
            continue
        code, path = line[:2], line[3:].strip().strip('"')
        if code.strip() == "":
            continue
        if code[0] == "M" or (code[0] in "AM" and code[1] in " M"):
            staged.append(path)
            continue
        rel = path.replace("\\", "/")
        ok_dir = rel.startswith(ALLOW_NEW_DIRS) or rel in ("build.py", "build.bat")
        ok_ext = os.path.splitext(rel)[1].lower() in ALLOW_NEW_EXTS
        junk = any(p in rel for p in JUNK_PATTERNS)
        (staged if (ok_dir and ok_ext and not junk) else skipped).append(path)
    return staged, skipped


def do_pr(title, dry_run):
    from datetime import datetime
    if shutil.which("gh") is None and not dry_run:
        fail("Falta gh CLI logueado para abrir el PR.", 3)
    staged, skipped = pr_candidates()
    if not staged:
        fail("Nada que commitear (arbol limpio o solo junk).", 3)
    branch = "build/" + datetime.now().strftime("%Y%m%d-%H%M%S")
    log(f"rama nueva: {branch} (master/main intactos, solo PR)")
    for p in skipped:
        log(f"  excluido del PR: {p}")
    if dry_run:
        log("[dry-run] commitearia estos archivos:")
        for p in staged:
            log(f"  + {p}")
        log(f"[dry-run] push -u origin {branch} + gh pr create --base master")
        return
    for cmd in (["git", "checkout", "-b", branch],
                ["git", "add", "--"] + staged,
                ["git", "commit", "-m", title],
                ["git", "push", "-u", "origin", branch]):
        r = run(cmd)
        if r.returncode != 0:
            fail(f"Fallo git: {' '.join(cmd[:3])}... ({r.stderr.strip()[:200]})", 3)
    r = run(["gh", "pr", "create", "--base", "master", "--head", branch, "--title", title,
             "--body", "Propuesto via build.py (suite Windows). Requiere aprobacion manual antes de merge."])
    if r.returncode != 0:
        fail(f"gh pr create fallo: {r.stderr.strip()[:300]}", 3)
    log(f"PR abierto: {r.stdout.strip()}")


def main():
    ap = argparse.ArgumentParser(description="Build suite Windows - Chebs Necromancy Enhanced")
    ap.add_argument("--config", default="Release", choices=["Release", "Debug"])
    ap.add_argument("--no-pull", action="store_true")
    ap.add_argument("--pr", metavar="TITULO", default=None)
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    step(1, 4, "Herramientas")
    check_tools()
    git_info()
    step(2, 4, "Resync con remoto")
    git_pull(args.no_pull)
    step(3, 4, "Detectando Valheim y BepInEx")
    valheim_dir = find_valheim()
    if not valheim_dir:
        fail("No encontre Valheim. Define VALHEIM_DIR o instala el juego.")
    bepinex_dir = find_bepinex(valheim_dir)
    if not bepinex_dir:
        fail("No encontre BepInEx. Define BEPINEX_DIR o instala BepInEx / perfil de mod manager.")
    step(4, 4, f"Compilando {args.config}")
    build_start = time.time()
    code, warnings, errors = build(args.config, valheim_dir, bepinex_dir)
    if code != 0 or errors:
        fail(f"Build fallo ({len(errors)} errores). Detalle en build.log.", 1)
    dll = report_dll(args.config, build_start)
    log(f"Listo. DLL fresco en:\n{dll}")
    # INVARIANTE: el PR solo se intenta si el build termino OK.
    # Cualquier fail() de arriba hace exit(1/2) antes de llegar aqui.
    if args.pr:
        log("=== PR (nunca push directo a master) ===")
        do_pr(args.pr, args.dry_run)


if __name__ == "__main__":
    main()
