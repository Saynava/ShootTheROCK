# RockWall Brush Tool — MVP spec

## Tavoite
Tehdään Unity-editoriin SceneView-työkalu, jolla voi maalata ja kumittaa destructible-seinää siveltimellä ilman tilemap-workflowta ja ilman että nykyinen seinän hajoamislogiikka rikkoutuu.

Käyttäjän kokemus:
- maalaa kallioon suoraan Scene viewssä
- kumita aukkoja pois
- näe tulos heti
- paina Play, ja seinä käyttäytyy edelleen nykyisen destructible-järjestelmän mukaan

## Nykytila
Nykyinen `RockWall` ei ole tilemap käyttäjän näkökulmasta, mutta sen sisäinen totuus on solupohjainen:
- `RockWall.cs` omistaa seinän soludatan ja vauriologiiikan
- `RockWallRuntimeGrid.cs` pilkkoo visual/collider-chunkeiksi
- `RockWallChunkRuntime.cs` piirtää ja rakentaa colliderit soluista

Tämä on hyvä asia. Sitä ei pidä kiertää, vaan editoribrushin pitää kirjoittaa samaan authoring-totuuteen.

Aiempi tuotantosuunta tukee tätä: seinän koko ja framing on jo siirretty pois transform-skaalauksesta datalähtöiseen malliin. Source: memory/2026-04-18.md#L18-L23

---

## Mitä muuttuu
Lisätään uusi authoring-kerros nykyisen runtime-seinän alle:
1. authoring-map, joka tallentaa mitä materiaalia missäkin solussa on
2. SceneView brush tool, joka maalaa tätä mappia
3. `RockWall`, joka rebuildaa previewn authoring-mapista

Näin käyttäjä saa vapaamman “maalauksen” tuntuman, mutta runtime pysyy yhteensopivana nykyisen destructible-järjestelmän kanssa.

---

## Tiedostot

### Uudet tiedostot
- `Assets/_Game/Data/Wall/RockWallAuthoringMap.cs`
- `Assets/_Game/Data/Wall/WallMaterialDefinition.cs`
- `Assets/_Game/Editor/RockWallPaintTool.cs`
- `Assets/_Game/Editor/RockWallPaintOverlay.cs`

### Muutettavat tiedostot
- `Assets/_Game/Scripts/RockWall.cs`
- `Assets/_Game/Editor/RockWallEditor.cs`
- mahdollisesti `Assets/_Game/Scripts/RockWallChunkRuntime.cs`

---

## Miksi juuri nämä tiedostot
- `RockWall.cs` on seinän oikea omistaja, joten brush-authoring pitää kytkeä siihen, ei scene-rootiin.
- `RockWallEditor.cs` on jo olemassa oleva authoring-sisäänkäynti. Sitä kannattaa laajentaa eikä tehdä neljättä erillistä workflowta.
- `RockWallRuntimeGrid.cs` / `RockWallChunkRuntime.cs` kannattaa pitää mahdollisimman koskemattomina MVP:ssä, jotta destruktiivinen runtime ei lähde leviämään.
- `WallMaterialDefinition.cs` antaa myöhemmin siistin polun rock -> sand -> water-tyyppisiin materiaaleihin ilman kovakoodattua sotkua.

---

## Vaihtoehdot

### Vaihtoehto A — maalaus suoraan runtime-taulukoihin
Brush päivittää suoraan `solidCells` / hp-taulukoita edit-modessa.

**Hyödyt**
- nopea ensimmäinen proto

**Haitat**
- huono persistence
- undo/redo vaikeutuu
- scene reload / resize muuttuu epäluotettavaksi
- monimateriaalit menevät nopeasti sotkuun

### Vaihtoehto B — erillinen authoring map + SceneView brush
Brush kirjoittaa authoring-dataan, ja `RockWall` rebuildaa siitä previewn.

**Hyödyt**
- säilyy editorissa siististi
- undo/redo onnistuu
- hyvä pohja materiaaleille
- ei riko runtime-destruktiota

**Haitat**
- vaatii vähän enemmän perustusta kuin A

### Vaihtoehto C — spline/shape authoring ja rasterointi
Käyttäjä piirtää vektorimuotoa, joka rasteroidaan seinädataksi.

**Hyödyt**
- teoriassa hieno authoring

**Haitat**
- turhan raskas MVP:lle
- lisää ylimääräisen muunnoskerroksen

---

## Suositus
**Valitaan Vaihtoehto B.**

Se on pienin oikea ratkaisu, joka:
- säilyttää nykyisen destruktiivisen seinärungon
- antaa oikean brush-authoring-kokemuksen
- mahdollistaa myöhemmät materiaalit ilman arkkitehtuurivelkaa

---

## MVP-scope

### Mukaan MVP:hen
- paint brush
- erase brush
- säädettävä brush size
- pehmeä round falloff
- SceneView preview ring
- Undo/Redo
- persistent authoring data
- yksi varsinainen materiaali: `Rock`
- yksi tyhjä tila: `Empty`
- rebuild preview editorissa

### Ei vielä MVP:hen
- oikea vesisimulaatio
- hiekan sortuminen
- bucket fill
- noise brushes
- layers
- erosion automations
- runtime-time terrain painting

---

## Datamalli

### `RockWallAuthoringMap`
ScriptableObject tai sub-asset, joka sisältää ainakin:
- `int width`
- `int height`
- `byte[] materialIds`

Tulkitut arvot MVP:ssä:
- `0 = Empty`
- `1 = Rock`

Mahdollinen apumetodi:
- `GetMaterialId(int row, int column)`
- `SetMaterialId(int row, int column, byte value)`
- `ResizeOrRecreate(int width, int height, bool preserveContent)`

### `WallMaterialDefinition`
Sisältää design-time materiaalitiedon:
- `string materialId`
- `Color previewColor`
- `bool isSolid`
- `float hitPointMultiplier`
- myöhemmin break-behavior / flow / diggable rules

MVP:ssä voi aluksi olla vain yksi default rock-materiali, mutta rakenne tehdään valmiiksi.

---

## `RockWall`-muutos MVP:ssä
`RockWall` saa authoring-lähteen, esim:
- `RockWallAuthoringMap authoringMap`

Ja editoripolun metodit, esim:
- `EnsureAuthoringMapMatchesCurrentResolution()`
- `PaintAuthoringCircle(Vector2 worldPoint, float radiusWorld, byte materialId, float hardness)`
- `RebuildWallFromAuthoringMap()`
- `ClearAuthoringMapTo(byte materialId)`

Tärkeä sääntö:
- authoring map = design-time alkuforma
- runtime hp / vaurio / destruction = edelleen runtime-taulukoissa

Eli Play Mode startissa runtime-taulut alustetaan authoring mapista.

---

## SceneView brush workflow

### Käyttö
- valitse `RockWall`
- aktivoi Paint Mode inspectorista
- Scene view:
  - `LMB` = paint selected material
  - `Shift + LMB` = erase to Empty
- hiiren alla näkyy brush preview

### Brush-logiikka
1. raycast / plane hit world-possa
2. world -> wall local
3. local -> grid
4. brush käy alueen solut läpi
5. falloff määrittää täyttyykö solu
6. authoring map päivittyy
7. preview rebuildataan

### Falloff
MVP:ssä käytetään pehmeää ympyräbrushia, ei kovaa laatikkoa.

Esim:
- keskellä täysi paint
- reunalla kynnys/falloff

Näin käyttäjälle syntyy oikea “sivellin” eikä tilemap-leima.

---

## Undo / persistence
Pakollinen sääntö:
- brush stroke alkaa `Undo.RecordObject(...)`
- scene/asset merkitään dirtyksi
- data pitää säilyä scene reloadin yli

Suositus MVP:hen:
- authoring map tallennetaan `RockWall`-komponentin omistamaksi sub-assetiksi tai serialisoiduksi asset-viitteeksi
- ei pelkkää transient runtime-arrayta

---

## Resize-sääntö
Tämä on tärkeä, ettei seinä mene vinoon.

Jos `worldWidth`, `worldHeight` tai `cellsPerUnit` muuttuu:
- ei tehdä hiljaista taikamuunnosta
- tarjotaan eksplisiittinen toiminto:
  - `Recreate Authoring Map`
  - myöhemmin ehkä `Resample From Existing`

MVP:ssä turvallisin sääntö on:
- authoring map sidotaan nykyiseen resoluutioon
- jos resoluutio ei täsmää, editori näyttää varoituksen + nappi uuden mapin luontiin

---

## Myöhempi materiaalilaajennus
MVP:n jälkeen sama runko mahdollistaa:
- Rock
- Sand
- Mud
- Ore

**Wateria ei pidä MVP:ssä käsitellä tavallisena seinämateriaalina**, koska se todennäköisesti tarvitsee oman simulaationsa eikä pelkkää staattista solid/non-solid-solua.

---

## Riskit
1. **Kaksi authoring-totuutta**
   - jos sekä vanha preview-logiikka että uusi map authoring jäävät eloon, seinä menee sekavaksi
   - ratkaisu: brush authoring tehdään suoraan `RockWall`-omisteiseksi workflowksi

2. **Huono editor performance**
   - koko seinän rebuild joka hiiren liikahduksesta voi tökkiä
   - ratkaisu MVP:ssä: throttle tai rebuild stroke-stepillä hallitusti

3. **Undo/redo hajoaa**
   - ratkaisu: stroke-kohtainen Undo + dirty-merkinnät

4. **Resoluution vaihto sotkee maalauksen**
   - ratkaisu: eksplisiittinen recreate warning, ei automaattista muunnosta MVP:ssä

---

## Verify-plan

### Verify 1 — authoring
- valitse `RockWall`
- maalaa yksi luola Scene viewssä
- kumita osa reunasta
- Undo toimii
- Redo toimii

### Verify 2 — persistence
- tallenna scene
- sulje / avaa scene
- maalaus säilyy

### Verify 3 — runtime
- paina Play
- seinä näyttää samalta kuin editorissa
- ammu seinää
- colliderit ja destruktio toimivat edelleen oikein

### Verify 4 — regression
- vanhat pocketit / prototype-startit eivät riko rebuild-polkuja
- kamera- ja scene-truth workflow pysyy elossa

---

## Toteutusjärjestys

### Phase 1
- `RockWallAuthoringMap`
- `RockWall` lukemaan authoring mapia
- `RebuildWallFromAuthoringMap()`

### Phase 2
- `RockWallEditor` inspectoriin paint mode controls
- `RockWallPaintTool` SceneView brush
- preview ring + paint/erase

### Phase 3
- cleanup + warnings resoluutioepäsopivuuksille
- rock-material defaultiksi `WallMaterialDefinition`

---

## Päätös
MVP rakennetaan `RockWall`-omisteiseksi SceneView brush -authoringiksi erillisellä authoring-mapilla.

Tämä on pienin ratkaisu, joka tuntuu käyttäjälle vapaalta maalaamiselta, mutta säilyttää nykyisen destructible-seinän ehjänä.

## Seuraava paras askel
Tee tämän speksin pohjalta **implementation pass 1**:
- authoring map
- `RockWall`-integraatio
- editor warning/resolution handling
- ei vielä täyttä SceneView brush UI:ta samassa passissa, jos halutaan riskit alas
