# Guía: yelmo caparazón en Unity (sin adivinanzas)

Estado del mod: listo y esperando. Si existe el fichero `chebgonaz-carapace`
junto al `chebgonaz` del mod, con dentro el objeto `ChebGonaz_HelmetCarapaceSkeleton`,
lo usa automáticamente (con `ItemDrop` injertado en carga + registro). Sin fichero:
todo sigue como hoy (fallback hierro). Cero cambios de config/DLL por tu parte
al terminar.

## 0. Requisitos
- `ChebsUnity` abriendo bien (doble clic `Abrir-ChebsUnity.bat`).
- AssetStudioModGUI (release en GitHub: `aelurum/AssetStudio`).

## 1. Extraer malla + texturas vanilla
1. AssetStudio → `File > Load file` → `D:/Steam.0/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles/c4210710`.
2. Filtro `CarapaceHelmet`: exporta la Mesh a FBX (`Export > Selected assets`).
3. Filtro `arapa`/`carapace` en Textures: exporta los PNG del casco (albedo/normal).
4. Opcional: captura del set vanilla en juego como referencia de color.

## 2. Importar a ChebsUnity
1. Copia el FBX + PNGs a `ChebsUnity/Assets/_chebgonaz/carapace/`.
2. Crea un Material Standard, asigna albedo/normal (metallic ~0.3 si no hay mapa).

## 3. Maniquí de referencia
- Menú `Chebs/5) Instanciar maniqui Tier4`: esqueleto + yelmo de hierro (posición BUENA conocida).
- Los scripts del juego salen como "missing": normal, los visuales quedan intactos.

## 4. Armar el prefab (la parte que importa)
1. Duplica la jerarquía del REF de hierro (`attach/bronzehelmet` + `collider`).
2. En la copia: MeshFilter.mesh = tu FBX; MeshRenderer.material = tu material.
3. Renombra el hijo de malla a `carapacehelmet` (sin la palabra `attach`).
4. Posiciónalo contra la cabeza del maniquí a ojo (mover/rotar/escalar el hijo).
5. SIN scripts en el prefab (se harían strip al compilar; el mod injerta el ItemDrop solo).
6. Arrastra a `Assets/_chebgonaz/carapace/`: fichero EXACTO
   `chebgonaz_helmetcarapaceskeleton.prefab`, objeto EXACTO
   `ChebGonaz_HelmetCarapaceSkeleton`.

## 5. Compilar y desplegar
1. Menú `Chebs/4) Compilar bundle chebgonaz-carapace` → `BundleOutput/chebgonaz-carapace`.
2. Copia ese fichero junto al `chebgonaz` en
   `.../profiles/Default/BepInEx/plugins/KUSHINI-ChebsNecromancyEnhanced/`.
3. `killall`, invoca UN caparazón fresco.

## 6. Señales de éxito en log
- `Unity carapace helmet loaded: ChebGonaz_HelmetCarapaceSkeleton.`
- `GraftHelmetItemDrop: ItemDrop grafted onto ...`
- Yelmo visible y ajustado; si queda alto/bajo/grande, mueve el hijo en Unity,
  recompila y repite (bucle de minutos).

## 7. Si algo falla
- Rosa: material/shader (usa Standard, reasigna texturas).
- Sale hierro: nombre de objeto o ruta del asset no exactos (revisar paso 4.6).
- `bundle created with newer version`: el juego corre Unity .75 y esto compila
  con .84; instala 6000.0.58/75 en el Hub y recompila.
- Rollback: borra `chebgonaz-carapace` y vuelve el hierro.
