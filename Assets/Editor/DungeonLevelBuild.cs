using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Two separately authored layouts using the project's original dungeon sprites.</summary>
public static class DungeonLevelBuild
{
    public static string PathFor(int index) => "Assets/Scenes/" + DungeonGame.LevelScenes[index] + ".unity";

    public static void RebuildAndValidate()
    {
        CreateLevels();
        ValidateAndPreview();
    }

    public static void ValidateAndPreview()
    {
        for(int index=1;index<3;index++)
        {
            EditorSceneManager.OpenScene(PathFor(index));
            Camera camera=Camera.main;
            camera.transform.position=new Vector3(0,1,-10);
            camera.orthographicSize=10.5f;
            var target=RenderTexture.GetTemporary(1200,800,24);
            var previous=RenderTexture.active;
            var texture=new Texture2D(1200,800,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;
                camera.Render();
                RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1200,800),0,0);
                texture.Apply();
                Directory.CreateDirectory("Artifacts");
                File.WriteAllBytes("Artifacts/"+DungeonGame.LevelScenes[index]+".png",texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null;
                RenderTexture.active=previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        DungeonValidation.Run();
    }

    public static void UpdateBuildSettings()
    {
        EditorBuildSettings.scenes = Enumerable.Range(0, DungeonGame.LevelScenes.Length)
            .Where(i => AssetDatabase.LoadAssetAtPath<SceneAsset>(PathFor(i)) != null)
            .Select(i => new EditorBuildSettingsScene(PathFor(i), true)).ToArray();
    }

    public static void EnsureLevels()
    {
        for (int i = 1; i < DungeonGame.LevelScenes.Length; i++)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PathFor(i)) == null) BuildLevel(i);
    }

    [MenuItem("LEA/Zwei zusätzliche Level erzeugen")]
    public static void CreateLevels()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Level bitte außerhalb des Spielmodus erzeugen.");
        var tileImporter=(TextureImporter)AssetImporter.GetAtPath("Assets/Sprites/PNG/walls_floor.png");
        var tileSettings=new TextureImporterSettings();
        tileImporter.ReadTextureSettings(tileSettings);
        if(tileSettings.spriteMeshType!=SpriteMeshType.FullRect)
        {
            tileSettings.spriteMeshType=SpriteMeshType.FullRect;
            tileImporter.SetTextureSettings(tileSettings);
            tileImporter.SaveAndReimport();
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DungeonBuild.ScenePath) == null) DungeonBuild.Prepare();
        BuildLevel(1);
        BuildLevel(2);
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(DungeonBuild.ScenePath);
        Debug.Log("LEA: Zisterne und Grabkammern erzeugt und in die Levelauswahl aufgenommen.");
    }

    private static void BuildLevel(int index)
    {
        bool water = index == 1;
        var scene = EditorSceneManager.OpenScene(DungeonBuild.ScenePath, OpenSceneMode.Single);
        // Save a separate scene before altering the copied environment.
        if (!EditorSceneManager.SaveScene(scene, PathFor(index))) throw new InvalidOperationException("Szene konnte nicht angelegt werden.");
        var game = UnityEngine.Object.FindFirstObjectByType<DungeonGame>();
        var camera = Camera.main;
        Transform gameplay = game.transform.root;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root != gameplay.gameObject && root != camera.transform.root.gameObject)
                UnityEngine.Object.DestroyImmediate(root);
        Transform oldBoundary = gameplay.Find("Südlicher Kartenabschluss");
        if (oldBoundary != null) UnityEngine.Object.DestroyImmediate(oldBoundary.gameObject);

        var cells = new HashSet<Vector2Int>();
        if (water)
        {
            Room(cells, -3, -7, 3, -3); Room(cells, -10, -2, -5, 3);
            Room(cells, 5, -2, 10, 3); Room(cells, -3, 4, 3, 8);
            Room(cells, -7, -5, 7, -4); Room(cells, -7, -4, -6, 5);
            Room(cells, 6, -4, 7, 5); Room(cells, -6, 4, 6, 5);
        }
        else
        {
            Room(cells, -10, -7, -6, -3); Room(cells, -10, 0, -5, 4);
            Room(cells, -2, -3, 3, 2); Room(cells, 6, -2, 11, 4);
            Room(cells, -3, 5, 3, 8); Room(cells, -8, -3, -7, 0);
            Room(cells, -5, 0, -1, 1); Room(cells, 3, -1, 6, 0);
            Room(cells, 7, 4, 8, 6); Room(cells, 2, 5, 8, 6);
        }
        var environment = new GameObject(water ? "Zisterne · Brücken und Wasserbecken" : "Grabkammern · Verschlungene Hallen").transform;
        Material material = game.player.GetComponent<SpriteRenderer>().sharedMaterial;
        int layer = LayerMask.NameToLayer("DungeonWalls");
        BuildArchitecture(cells,water,environment,material,layer);

        Vector2 spawn = water ? new Vector2(0,-6) : new Vector2(-8,-6);
        Place(game.player.transform, spawn);
        Vector2[] guardians = water
            ? new[] { new Vector2(-8,1), new Vector2(8,1), new Vector2(0,6) }
            : new[] { new Vector2(-8,2), new Vector2(9,2), new Vector2(0,6) };
        Vector2[] chests = water
            ? new[] { new Vector2(-7.8f,2.5f), new Vector2(7.8f,2.5f), new Vector2(-1,7) }
            : new[] { new Vector2(-7.8f,3.4f), new Vector2(8.7f,3.4f), new Vector2(-1,7) };
        var sealChests = UnityEngine.Object.FindObjectsByType<DungeonInteractable>(FindObjectsSortMode.None)
            .Where(i => i.kind == DungeonInteractable.ItemKind.SealChest).OrderBy(i => i.name).ToArray();
        for (int i=0;i<sealChests.Length;i++)
        {
            Place(sealChests[i].transform, chests[i]);
            Place(sealChests[i].guardian.transform, guardians[i]);
            sealChests[i].guardian.guardianLeash = 2.8f;
        }
        var guards = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)
            .Where(e => !e.isGuardian).OrderBy(e => e.name).ToArray();
        Place(guards[0].transform, water ? new Vector2(-6,-1) : new Vector2(-3,1));
        Place(guards[1].transform, water ? new Vector2(6,3) : new Vector2(6,0));
        var items = UnityEngine.Object.FindObjectsByType<DungeonInteractable>(FindObjectsSortMode.None);
        var oldExit=items.Single(i => i.kind == DungeonInteractable.ItemKind.Exit);
        UnityEngine.Object.DestroyImmediate(oldExit.gameObject);
        var exit=Prefab("Door",new Vector2(2,8.65f),.65f,gameplay,300);
        exit.name="Nordtor · Door Prefab";
        var exitUse=new GameObject("Nordtor · Interaktion");
        exitUse.transform.SetParent(gameplay); exitUse.transform.position=new Vector3(2,7.8f,0);
        exitUse.AddComponent<DungeonInteractable>().kind=DungeonInteractable.ItemKind.Exit;
        var potions = items.Where(i => i.kind == DungeonInteractable.ItemKind.Potion).ToArray();
        Place(potions[0].transform, water ? new Vector2(-6,-4) : new Vector2(-7,-1));
        Place(potions[1].transform, water ? new Vector2(6,4) : new Vector2(2,1));
        var traps = UnityEngine.Object.FindObjectsByType<DungeonTrap>(FindObjectsSortMode.None);
        for(int i=0;i<traps.Length;i++)
        {
            float phase=traps[i].phaseOffset;
            UnityEngine.Object.DestroyImmediate(traps[i].gameObject);
            Vector2 point=i==0 ? (water ? new Vector2(-6,4) : new Vector2(4,0))
                : (water ? new Vector2(6,-4) : new Vector2(7,5));
            var trap=Prefab("Plate_Trap",point,.42f,gameplay,8);
            trap.AddComponent<DungeonTrap>().phaseOffset=phase;
        }

        Vector2[] decorations = water
            ? new[] { new Vector2(-9.8f,3),new Vector2(-5.2f,3),new Vector2(5.2f,3),new Vector2(9.8f,3),new Vector2(-2.6f,8),new Vector2(-2.6f,-6.8f),new Vector2(2.6f,-6.8f) }
            : new[] { new Vector2(-10,4),new Vector2(11,4),new Vector2(-9.8f,-3.2f) };
        foreach (Vector2 point in decorations)
        {
            var torch=Prefab("Big_Torch",point,.4f,environment,1000-Mathf.RoundToInt(point.y*100));
            var glow= new GameObject("Fackellicht").AddComponent<Light2D>();
            glow.transform.SetParent(torch.transform);glow.transform.position=point;
            glow.lightType=Light2D.LightType.Point;
            glow.color=new Color(.35f,.85f,.82f);glow.intensity=.5f;
            glow.pointLightOuterRadius=2.7f;glow.pointLightInnerRadius=.35f;
        }
        if (!water)
        {
            foreach(Vector2 point in new[] {new Vector2(-5.9f,3.8f),new Vector2(6.3f,3.2f),new Vector2(-2.4f,7.8f)})
                Prefab("Statue",point,.4f,environment,1000-Mathf.RoundToInt(point.y*100));
            foreach(Vector2 point in new[] {new Vector2(-9.6f,.9f),new Vector2(-9.6f,3.1f),new Vector2(10.4f,-.8f),new Vector2(10.4f,1.4f)})
            {
                var coffin = new GameObject("Steinsarkophag");
                coffin.transform.SetParent(environment);
                coffin.transform.localScale=Vector3.one*.45f;
                var renderer=coffin.AddComponent<SpriteRenderer>();
                renderer.sprite=Sprite("coffins",0); renderer.sharedMaterial=material;
                // Align the foot of the sarcophagus to the wall-side grave niche.
                CenterPrefab(coffin,point);
                renderer.sortingOrder=1000-Mathf.RoundToInt(point.y*100);
                coffin.layer=layer;
                var collider=coffin.AddComponent<BoxCollider2D>();
                collider.size=new Vector2(.65f,.65f)/.45f;
                collider.offset=renderer.sprite.bounds.center;
                Prefab("Big_Candle_1",point+new Vector2(.65f,.6f),.4f,environment,renderer.sortingOrder+1);
            }
            Prefab("Scull",new Vector2(.5f,-2.7f),.55f,environment,9);
        }
        var light=UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None).Single(l=>l.lightType==Light2D.LightType.Global);
        light.color = water ? new Color(.78f,.94f,1f) : new Color(1f,.87f,.72f);
        light.intensity=.82f;
        camera.orthographicSize = 5.8f;
        camera.transform.position = new Vector3(spawn.x, spawn.y, -10);
        var follow = camera.GetComponent<DungeonCamera>() ?? camera.gameObject.AddComponent<DungeonCamera>();
        follow.target = game.player.transform;
        follow.minimum = new Vector2(-13,-9); follow.maximum = new Vector2(14,11);

        items=UnityEngine.Object.FindObjectsByType<DungeonInteractable>(FindObjectsSortMode.None);
        ValidateLayout(cells, spawn, items);
        ValidateAssetUsage(environment);
        foreach(var root in scene.GetRootGameObjects())
            foreach(var component in root.GetComponentsInChildren<Component>(true))
                if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        Physics2D.SyncTransforms();
        foreach (var actor in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Select(e => e.transform).Append(game.player.transform))
            if (Physics2D.OverlapCircle(actor.position,.29f,1 << layer) != null)
                throw new InvalidOperationException(actor.name + " startet in einer Wand.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, PathFor(index))) throw new InvalidOperationException("Level konnte nicht gespeichert werden.");
        Debug.Log(DungeonGame.LevelNames[index] + ": " + cells.Count + " verbundene Bodenfelder; alle Truhen, Tränke und Ausgang erreichbar.");
    }

    private const float ArtScale=.65f;

    private static void BuildArchitecture(HashSet<Vector2Int> cells,bool water,Transform root,Material material,int layer)
    {
        Transform floorRoot=Group("Bodenplatten",root), edgeRoot=Group("Zusammenhängende Mauern und Ufer",root);
        Transform detailRoot=Group("Bodenmuster und Wasser",root), collisionRoot=Group("Begehbarer Grundriss",root);
        // The plain original floor region has no atlas gutters, so the large background has no seams.
        Strip(Sprite("walls_floor",63),new Rect(-16,-11,32,24),
            detailRoot,material,-100,water?new Color(.34f,.72f,.79f):new Color(.055f,.06f,.075f));

        foreach(var row in cells.GroupBy(c=>c.y))
            foreach(var run in Runs(row.Select(c=>c.x)))
            {
                var floor=Prefab("Floor_Tiles",Vector2.zero,ArtScale,floorRoot,0);
                floor.name="Bodenreihe "+row.Key+" / "+run.x;
                FitPrefab(floor,new Vector2((run.x+run.y)/2f,row.Key),new Vector2(run.y-run.x+1,1));
            }

        // Keep collision at the actual shoreline; artwork extends outward into solid space.
        foreach(var cell in cells)
            foreach(var direction in new[] {Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                if(!cells.Contains(cell+direction))
                {
                    var barrier=new GameObject("Rand "+cell+" "+direction);
                    barrier.transform.SetParent(collisionRoot);
                    barrier.transform.position=(Vector2)cell+(Vector2)direction*.55f;
                    barrier.layer=layer;
                    barrier.AddComponent<BoxCollider2D>().size=direction.x==0?new Vector2(1.04f,.1f):new Vector2(.1f,1.04f);
                }

        foreach(bool north in new[] {true,false})
        {
            Vector2Int direction=north?Vector2Int.up:Vector2Int.down;
            foreach(var row in cells.Where(c=>!cells.Contains(c+direction)).GroupBy(c=>c.y))
                foreach(var run in Runs(row.Select(c=>c.x)))
                {
                    Sprite sprite=Sprite("walls_floor",north&&!water?54:34);
                    float height=sprite.bounds.size.y*ArtScale;
                    float y=north?row.Key+.5f:row.Key-.5f-height;
                    Rect area=new Rect(run.x-.5f,y,run.y-run.x+1,height);
                    Strip(sprite,area,edgeRoot,material,5,Color.white);
                    // A thin existing cap joins the front wall to the floor without repeating square borders.
                    if(north)
                        Strip(Sprite("walls_floor",64),new Rect(area.x,area.yMax-.06f,area.width,.08f),edgeRoot,material,6,Color.white);
                }
        }
        foreach(bool left in new[] {true,false})
        {
            Vector2Int direction=left?Vector2Int.left:Vector2Int.right;
            foreach(var column in cells.Where(c=>!cells.Contains(c+direction)).GroupBy(c=>c.x))
                foreach(var run in Runs(column.Select(c=>c.y)))
                {
                    Sprite sprite=Sprite("walls_floor",left?52:51);
                    float width=sprite.bounds.size.x*ArtScale;
                    Strip(sprite,new Rect(left?column.Key-.5f-width:column.Key+.5f,run.x-.5f,width,run.y-run.x+1),
                        edgeRoot,material,6,Color.white);
                }
        }

        if(water)
        {
            // One short crossing uses the complete original bridge. The other crossings stay open.
            Vector2 crossing=new Vector2(-4.5f,-4.5f);
            Prefab("Brige",crossing,ArtScale,root,7);
            var deck=Prefab("Floor_Tiles",crossing,ArtScale,root,8);
            deck.name="Brückendeck · Durchgang";
            FitPrefab(deck,crossing,new Vector2(1.35f,1.1f));
            for(int side=-1;side<=1;side+=2)
            {
                var pier=new GameObject("Brückenpfeiler · Kollision");
                pier.transform.SetParent(collisionRoot);pier.transform.position=crossing+Vector2.up*(side*1.1f);
                pier.layer=layer;pier.AddComponent<BoxCollider2D>().size=new Vector2(1.25f,1f);
                Detail(Sprite("plates",10),crossing+Vector2.right*(side*.33f),ArtScale,root,material,9,new Color(1,1,1,.36f));
            }
            // Paired, attached supports establish the edge of the central water basin.
            Prefab("Piller_Water_Animated",new Vector2(-3.8f,.3f),.45f,root,4);
            Prefab("Piller_Water_Animated",new Vector2(3.8f,.3f),.45f,root,4);
            foreach(Vector2 p in new[] {new Vector2(-1.8f,0),new Vector2(1.5f,2),new Vector2(.2f,-1.6f),new Vector2(-11.8f,-1),new Vector2(12,-4),new Vector2(-9,6.3f),new Vector2(8,8.4f)})
                Prefab("Bubble",p,.16f,detailRoot,-90);
        }
        Paving(cells,detailRoot,material);
        // Sparse, differently shaped wear stays at the same pixel scale as the rest of the art.
        Vector2[] wear=water?new[] {new Vector2(-9,-1),new Vector2(9,2),new Vector2(2,-6)}
            :new[] {new Vector2(-6.2f,1),new Vector2(2,-2),new Vector2(7,3),new Vector2(-2,6)};
        for(int i=0;i<wear.Length;i++)
            Detail(Sprite("walls_floor",12+i%4),wear[i],ArtScale,detailRoot,material,2,new Color(1,1,1,.45f));
    }


    private static Transform Group(string name,Transform parent)
    {
        var group=new GameObject(name).transform; group.SetParent(parent); return group;
    }

    // Inclusive contiguous integer spans, shared by floors and all four perimeter directions.
    private static IEnumerable<Vector2Int> Runs(IEnumerable<int> coordinates)
    {
        int[] values=coordinates.Distinct().OrderBy(v=>v).ToArray();
        if(values.Length==0) yield break;
        int start=values[0],last=start;
        for(int i=1;i<values.Length;i++)
        {
            if(values[i]!=last+1) {yield return new Vector2Int(start,last);start=values[i];}
            last=values[i];
        }
        yield return new Vector2Int(start,last);
    }

    private static void Paving(HashSet<Vector2Int> cells,Transform parent,Material material)
    {
        // Original individual flagstones share the actors' pixel scale. Missing stones and
        // small shape variations break repetition without introducing differently scaled textures.
        int[] variants={0,2,3,4,6,7,8,10,12,14};
        for(int y=-11;y<=13;y++) for(int x=-17;x<=18;x++)
        {
            Vector2 point=new Vector2(x*.68f,y*.68f);
            if(!cells.Contains(Vector2Int.RoundToInt(point-new Vector2(.37f,.37f))) ||
                !cells.Contains(Vector2Int.RoundToInt(point+new Vector2(.37f,.37f))) ||
                !cells.Contains(Vector2Int.RoundToInt(point+new Vector2(-.37f,.37f))) ||
                !cells.Contains(Vector2Int.RoundToInt(point+new Vector2(.37f,-.37f)))) continue;
            int seed=Mathf.Abs(x*31+y*17);
            if(seed%11==0) continue;
            Detail(Sprite("plates",variants[seed%variants.Length]),point,ArtScale,parent,material,1,new Color(1,1,1,.36f));
        }
    }

    private static GameObject Detail(Sprite sprite,Vector2 center,float scale,Transform parent,Material material,int order,Color tint)
    {
        var go=new GameObject(sprite.name);
        go.transform.SetParent(parent);go.transform.localScale=Vector3.one*scale;
        var renderer=go.AddComponent<SpriteRenderer>(); renderer.sprite=sprite;renderer.sharedMaterial=material;
        renderer.sortingOrder=order;renderer.color=tint;CenterPrefab(go,center);
        return go;
    }

    private static void Strip(Sprite sprite,Rect area,Transform parent,Material material,int order,Color tint)
    {
        var go=Detail(sprite,area.center,ArtScale,parent,material,order,tint);
        FitPrefab(go,area.center,area.size);
    }

    private static GameObject Prefab(string name,Vector2 center,float scale,Transform parent,int order)
    {
        string path="Assets/Prefabs/"+name+".prefab";
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(asset==null) throw new InvalidOperationException("Prefab fehlt: "+path);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.transform.SetParent(parent);
        go.transform.localScale=Vector3.one*scale;
        var renderer=go.GetComponent<SpriteRenderer>();
        var animation=go.GetComponent<SpriteAnimationLoop>();
        // Preserve the authored prefab and remap legacy Tiled_files references on this instance.
        // Both folders contain the same artwork; match exact source rectangles when names differ.
        if(animation!=null && animation.sprites!=null)
            animation.sprites=animation.sprites.Select(PngSprite).ToArray();
        if(animation!=null && animation.sprites!=null && animation.sprites.Length>0)
            renderer.sprite=animation.sprites[0];
        else renderer.sprite=PngSprite(renderer.sprite);
        if(renderer.sprite==null) throw new InvalidOperationException("Prefab ohne gültiges Sprite: "+path);
        renderer.sortingOrder=order;
        CenterPrefab(go,center);
        return go;
    }

    private static Sprite PngSprite(Sprite sprite)
    {
        if(sprite==null) throw new InvalidOperationException("Vorhandenes Prefab enthält einen fehlenden Sprite-Verweis.");
        string path=AssetDatabase.GetAssetPath(sprite);
        if(path.StartsWith("Assets/Sprites/PNG/",StringComparison.Ordinal)) return sprite;
        string pngPath="Assets/Sprites/PNG/"+Path.GetFileName(path);
        if(Path.GetFileName(path)=="Water_coasts_animation.png" && sprite.name.StartsWith("Slab_",StringComparison.Ordinal))
        {
            // The Tiled export adds margins between atlas cells. These are its six
            // corresponding unpadded shore-platform frames in the original PNG sheet.
            int frame=int.Parse(sprite.name.Substring(5))-1;
            int[] frames={4,5,8,9,12,13};
            if(frame>=0 && frame<frames.Length) return Sprite("Water_coasts_animation",frames[frame]);
        }
        var match=AssetDatabase.LoadAllAssetsAtPath(pngPath).OfType<Sprite>()
            .FirstOrDefault(s=>s.rect==sprite.rect);
        if(match==null) throw new InvalidOperationException("Kein passendes PNG-Sprite für "+path+" / "+sprite.name);
        return match;
    }

    private static void CenterPrefab(GameObject go,Vector2 center)
    {
        var renderer=go.GetComponent<SpriteRenderer>();
        go.transform.position+=new Vector3(center.x,center.y,0)-renderer.bounds.center;
    }

    private static void FitPrefab(GameObject go,Vector2 center,Vector2 size)
    {
        var renderer=go.GetComponent<SpriteRenderer>();
        go.transform.localScale=Vector3.one*ArtScale;
        renderer.drawMode=SpriteDrawMode.Tiled;
        renderer.size=size/ArtScale;
        CenterPrefab(go,center);
    }

    private static void ValidateAssetUsage(Transform environment)
    {
        var prefabs=new HashSet<string>();
        foreach(var renderer in environment.GetComponentsInChildren<SpriteRenderer>())
        {
            string source=AssetDatabase.GetAssetPath(renderer.sprite);
            if(!source.StartsWith("Assets/Sprites/PNG/",StringComparison.Ordinal))
                throw new InvalidOperationException("Levelgrafik stammt nicht aus Sprites/PNG: "+renderer.name+" / "+source);
            string prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject);
            if(!string.IsNullOrEmpty(prefab)) prefabs.Add(prefab);
        }
        foreach(var animation in environment.GetComponentsInChildren<SpriteAnimationLoop>())
            if(animation.sprites==null || animation.sprites.Any(s=>s==null || !AssetDatabase.GetAssetPath(s).StartsWith("Assets/Sprites/PNG/",StringComparison.Ordinal)))
                throw new InvalidOperationException("Animation enthält fehlende oder fremde Sprites: "+animation.name);
        if(prefabs.Count<4) throw new InvalidOperationException("Zu wenige vorhandene Prefab-Bauteile im Level.");
        Debug.Log(environment.name+" verwendet PNG-Grafiken und vorhandene Prefabs: "+string.Join(", ",prefabs.OrderBy(p=>p)));
    }

    private static void ValidateLayout(HashSet<Vector2Int> cells, Vector2 spawn, DungeonInteractable[] items)
    {
        var reached = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(Vector2Int.RoundToInt(spawn));
        while(queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            if (!cells.Contains(cell) || !reached.Add(cell)) continue;
            foreach (Vector2Int d in new[] {Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right}) queue.Enqueue(cell+d);
        }
        if (reached.Count != cells.Count || items.Any(i => !reached.Contains(Vector2Int.RoundToInt(i.transform.position))))
            throw new InvalidOperationException("Level enthält unerreichbare Bereiche oder Gegenstände.");
    }

    private static void Room(HashSet<Vector2Int> cells,int left,int bottom,int right,int top)
    {
        for(int y=bottom;y<=top;y++) for(int x=left;x<=right;x++) cells.Add(new Vector2Int(x,y));
    }

    private static Sprite Sprite(string atlas,int index) => AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/PNG/"+atlas+".png")
        .OfType<Sprite>().Single(s => s.name == atlas+"_"+index);

    private static void Place(Transform target,Vector2 point)
    {
        target.position = new Vector3(point.x,point.y,0);
        var renderer=target.GetComponent<SpriteRenderer>();
        if(renderer!=null) renderer.sortingOrder=target.GetComponent<DungeonTrap>()!=null?8:1000-Mathf.RoundToInt(point.y*100);
        var body=target.GetComponent<Rigidbody2D>();
        if(body!=null) body.position=point;
    }
}
