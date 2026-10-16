# Casco Caparazón custom + armas hierro negro (Tier Caparazón)

## Estado actual

- Armas Tier4: HECHO POR CÓDIGO (sin Unity). `WarriorTier4Weapons`
  default `SwordBlackmetal,AxeBlackMetal,MaceNeedle`: al cargar se
  reemplaza el `m_randomWeapon` bakeado del prefab por los modelos vanilla
  del jugador (`BasePlugin.cs`). Vacío = conserva las custom del bundle.
  Caparazón hereda el mismo pool (mismo prefab Tier4).
  Diagnóstico `WarriorTier4WeaponDiag=true` vuelca en el log los campos
  de fábrica antes del swap + si cada nombre vanilla resuelve.
- Casco: PENDIENTE UNITY. `ArmorCarapaceHelmetPrefab` default =
  `ChebGonaz_HelmetCarapaceSkeleton`, escala `1.0` (solo equipa).
  Si el prefab custom aún no existe en el bundle, fallback automático a
  `ChebGonaz_HelmetBlackIronSkeleton` para no dejar calvo (`SkeletonMinion.cs`).

## Qué son los "triggers" que faltan

En los cascos que sí encajan (`ChebGonaz_HelmetBlackIronSkeleton`):
- `ItemDrop` + `ZNetView` + `ZSyncTransform` en raíz.
- Hijos: `attach` / `Helmet_attach` (punto de anclaje a `Head`),
  malla (`bronzehelmet`, `blackmetalchest`, etc.) como `SkinnedMeshRenderer`
  con `rootBone = Head` y `bones[]` apuntando al armature del esqueleto,
  no al del jugador. Eso es lo que hay que replicar.
- Sin esos huesos bien asignados el casco no sigue la cabeza al bailar/atacar.

## Ruta Unity (proyecto original perdido: `chebs-necromancy-unity` da 404)

1. Nuevo proyecto Unity `6000.0.84f1` (tienes `2021.3.45f2` y `6000.0.84f1`
   en `C:/Program Files/Unity/Hub/Editor`). Usa 6000, que es con lo que
   se compiló el bundle actual.
2. Instala Jotunn Unity package + referencia a
   `D:/Steam.0/steamapps/common/Valheim/valheim_Data/Managed/*.dll`
   (tienes Valheim + BepInEx en `D:/Steam.0/...`).
3. Extrae referencia: del bundle saca `chebgonaz_helmetblackironskeleton.prefab`
   como plantilla (jerarquía + `ItemDrop` + anclajes). Para la malla Carapace,
   exporta `HelmetCarapace` vanilla con AssetStudio/UnityPy desde
   `valheim_Data` o duplica el mesh en el editor con el Valheim Asset DB.
4. Nuevo prefab `ChebGonaz_HelmetCarapaceSkeleton`:
   - Duplica `ChebGonaz_HelmetBlackIronSkeleton`, cambia solo el mesh/material
     a Carapace, mantén `rootBone`, `bones[]`, `Helmet_attach`, colliders.
   - `ItemDrop.m_itemData.m_shared`: `m_armor` como el iron, nombre
     `$item_chebgonaz_carapacehelmet` (añadir a `Translations/`).
   - Añade al AssetBundle `chebgonaz` en la misma carpeta que el resto
     (`assets/_chebgonaz/carapace/...`), rebuild bundle.
5. Prueba: pon el bundle nuevo en
   `D:/Steam.0/steamapps/common/Valheim/BepInEx/plugins/`,
   invoca Warrior con `BlackMetal + Carapace>=5` + `Shift`, verifica que el
   casco sigue a la cabeza en idle/ataque/baile (`J`). Si flota, revisa
   `bones[]` (deben ser los del `_skeleton_base`, no `player`).

## Sin Unity (fallback, ya funciona hoy)

Poner en config `ArmorCarapaceHelmetPrefab = ChebGonaz_HelmetBlackIronSkeleton`
mantiene estética correcta sin trabajo Unity, a la espera del prefab custom.
