using UnityEngine;

/// <summary>Game UI drawn from the original PNG_UI sprite atlases, with crisp sliced borders.</summary>
public sealed class DungeonHUD : MonoBehaviour
{
    [Header("PNG_UI · Fenster und Schaltflächen")]
    public Sprite panelSprite, buttonNormal, buttonHover, buttonPressed, characterFrame, actionPanel;
    [Header("PNG_UI · Symbole")]
    public Sprite sealIcon, coinIcon, swordIcon, healthIcon, movementIcon, soundOnIcon, soundOffIcon, starIcon, defeatIcon;
    [Header("HUD · Festes Spielerporträt")]
    public Sprite playerPortrait;
    private static readonly Color Ink = new Color(.20f,.15f,.11f);
    private static readonly Color Muted = new Color(.40f,.31f,.20f);
    private static readonly Color Green = new Color(.18f,.38f,.25f);
    private static readonly Color Cream = new Color(1,.91f,.70f);
    private GUIStyle label, centered, button;
    private Font pixelFont, pixelButtonFont;

    private void OnEnable() => Font.textureRebuilt += OnFontTextureRebuilt;
    private void OnDisable() => Font.textureRebuilt -= OnFontTextureRebuilt;

    private void OnFontTextureRebuilt(Font font)
    {
        if (font == pixelFont || font == pixelButtonFont) UsePointFiltering(font);
    }

    private static void UsePointFiltering(Font font)
    {
        if (font != null && font.material != null && font.material.mainTexture != null)
            font.material.mainTexture.filterMode = FilterMode.Point;
    }

    public static bool BlocksWorldPointer(Vector2 screenPosition)
    {
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        Vector2 p = new Vector2((screenPosition.x - (Screen.width - 1280 * scale) / 2) / scale,
            (Screen.height - screenPosition.y - (Screen.height - 720 * scale) / 2) / scale);
        return new Rect(20,17,275,102).Contains(p)
            || new Rect(735,21,523,83).Contains(p)
            || new Rect(1090,641,164,43).Contains(p);
    }

    private void OnGUI()
    {
        var game = DungeonGame.Instance;
        if (game == null) return;
        EnsureStyles();
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        GUI.depth = -100; GUI.color = Color.white; GUI.matrix = Matrix4x4.identity;
        if (game.State != DungeonGame.GameState.Playing)
            Fill(new Rect(0,0,Screen.width,Screen.height), new Color(.035f,.055f,.065f,.72f));
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2,0),
            Quaternion.identity,Vector3.one*scale);
        switch (game.State)
        {
            case DungeonGame.GameState.Menu: DrawMenu(game); break;
            case DungeonGame.GameState.Playing: DrawGame(game); break;
            case DungeonGame.GameState.Paused: DrawPause(game); break;
            case DungeonGame.GameState.Won: DrawResult(game,true); break;
            case DungeonGame.GameState.Lost: DrawResult(game,false); break;
        }
        GUI.matrix=previousMatrix; GUI.color=previousColor;
    }

    private void EnsureStyles()
    {
        if (label != null) return;
        pixelFont=Resources.Load<Font>("Fonts/PixelifySans-Regular");
        pixelButtonFont=Resources.Load<Font>("Fonts/PixelifySans-Bold");
        UsePointFiltering(pixelFont);
        UsePointFiltering(pixelButtonFont);
        label=new GUIStyle { font=pixelFont,fontSize=20,alignment=TextAnchor.MiddleLeft,wordWrap=true };
        centered=new GUIStyle(label) { alignment=TextAnchor.MiddleCenter };
        button=new GUIStyle(centered) { font=pixelButtonFont,fontSize=23,wordWrap=false };
        button.normal.textColor=button.hover.textColor=button.active.textColor=button.focused.textColor=Ink;
    }

    private void DrawMenu(DungeonGame g)
    {
        Panel(new Rect(278,44,724,630));
        Icon(sealIcon,new Rect(357,92,57,62)); Icon(sealIcon,new Rect(866,92,57,62));
        Text(new Rect(416,73,448,96),"LEA",80,true);
        Text(new Rect(330,174,620,36),"DIE VERGESSENE KRYPTA",25,true,Green);
        Rule(358,224,564);
        for (int i=0;i<DungeonGame.LevelNames.Length;i++)
        {
            string caption=(i==g.LevelIndex?"• ":"") + DungeonGame.LevelNames[i];
            if(Button(new Rect(315+i*218,254,214,48),caption)) g.SelectLevel(i);
        }
        if (Button(new Rect(369,335,542,61),"Krypta betreten")) g.StartRun();
        if (Button(new Rect(369,414,263,48),g.Muted?"Ton aus":"Ton an",g.Muted?soundOffIcon:soundOnIcon)) g.ToggleMute();
        if (Button(new Rect(648,414,263,48),"Beenden")) g.QuitGame();
        Rule(358,493,564);
        Control(351,515,"WASD / PFEILE","Bewegen",movementIcon);
        Control(665,515,"LEER / MAUS","Angreifen",swordIcon);
        Control(351,574,"E","Truhe / Nordtor",sealIcon);
        Control(665,574,"SHIFT · ESC","Sprinten · Pause",movementIcon);
    }

    private void DrawGame(DungeonGame g)
    {
        int hp=g.Player!=null?g.Player.CurrentHealth:0, max=g.Player!=null?g.Player.MaxHealth:6;
        DrawSprite(characterFrame,new Rect(20,17,275,102));
        // A separate idle portrait keeps HUD framing independent of movement and attack frames.
        DrawPlayerPortrait();
        Fill(new Rect(124,44,124,9),new Color(.19f,.09f,.08f));
        Fill(new Rect(124,44,124*hp/Mathf.Max(1f,max),9),new Color(.84f,.28f,.22f));
        for(int i=1;i<max;i++) Fill(new Rect(124+124f*i/max,44,2,9),new Color(.24f,.16f,.09f));
        Metric(new Rect(735,21,169,83),sealIcon,"SIEGEL",g.SealCount+" / 3");
        Metric(new Rect(918,21,155,83),coinIcon,"MÜNZEN",g.CoinCount.ToString());
        Metric(new Rect(1087,21,171,83),swordIcon,"BESIEGT",g.DefeatedEnemies+" / "+g.TotalEnemies);
        if(!string.IsNullOrEmpty(g.InteractionPrompt))
        {
            Panel(new Rect(319,555,642,56));
            Text(new Rect(339,566,602,34),g.InteractionPrompt,18,true,Green);
        }
        if(Button(new Rect(1090,641,164,43),"Pause · ESC")) g.Pause();
    }

    private void DrawPause(DungeonGame g)
    {
        Panel(new Rect(392,87,496,551));
        Icon(sealIcon,new Rect(615,116,49,54));
        Text(new Rect(430,184,420,49),"ATEMPAUSE",35,true);
        Text(new Rect(431,239,418,32),DungeonGame.LevelNames[g.LevelIndex],18,true,Muted);
        if(Button(new Rect(459,292,362,53),"Weiterkämpfen")) g.Resume();
        if(Button(new Rect(459,361,362,47),"Neu starten")) g.RestartRun();
        if(Button(new Rect(459,424,362,47),"Hauptmenü")) g.ReturnToMenu();
        if(Button(new Rect(459,487,362,47),g.Muted?"Ton einschalten":"Ton ausschalten",g.Muted?soundOffIcon:soundOnIcon)) g.ToggleMute();
    }

    private void DrawPlayerPortrait()
    {
        if(playerPortrait==null) return;
        // Frame the body of the idle sprite, excluding the sword's extra width.
        // The fixed crop is centered in the circular opening at (71, 68).
        Rect source=playerPortrait.rect;
        Rect crop=new Rect(source.x+source.width/30f,source.y+source.height*.1f,
            source.width*16f/30f,source.height*22f/30f);
        GUI.DrawTextureWithTexCoords(new Rect(47,35,48,66),playerPortrait.texture,
            new Rect(crop.x/playerPortrait.texture.width,crop.y/playerPortrait.texture.height,
                crop.width/playerPortrait.texture.width,crop.height/playerPortrait.texture.height));
    }

    private void DrawResult(DungeonGame g,bool won)
    {
        Panel(new Rect(234,68,812,588));
        if(won)
        {
            Icon(starIcon,new Rect(515,107,61,60)); Icon(starIcon,new Rect(601,90,78,76)); Icon(starIcon,new Rect(704,107,61,60));
        }
        else Icon(defeatIcon,new Rect(604,98,72,72));
        Text(new Rect(278,194,724,50),won?"DER KRYPTA ENTKOMMEN":"VON SCHATTEN BESIEGT",33,true,won?Green:new Color(.73f,.18f,.17f));
        Text(new Rect(301,255,678,63),won?"Die drei Seelensiegel sind vereint.\nDas Nordtor öffnet deinen Weg ins Licht.":"Die Wächter bewahren ihre Geheimnisse.\nVersuche es erneut – jedes Siegel heilt dich.",20,true,Muted);
        Rule(295,344,690);
        ResultMetric(293,sealIcon,"SIEGEL",g.SealCount+" / 3");
        ResultMetric(467,coinIcon,"MÜNZEN",g.CoinCount.ToString());
        ResultMetric(641,swordIcon,"BESIEGT",g.DefeatedEnemies+" / "+g.TotalEnemies);
        ResultMetric(815,null,"ZEIT",TimeText(g.ElapsedTime));
        if(Button(new Rect(305,512,324,56),won&&g.HasNextLevel?"Nächstes Level":won?"Noch einmal spielen":"Erneut versuchen")) g.ContinueAfterResult();
        if(Button(new Rect(650,512,324,56),"Hauptmenü")) g.ReturnToMenu();
        Text(new Rect(302,596,676,27),won&&g.HasNextLevel?"EINGABE  Nächstes Level":"EINGABE  Neu starten",15,true,Muted);
    }

    private void Metric(Rect r,Sprite icon,string caption,string value)
    {
        Panel(r); Icon(icon,new Rect(r.x+14,r.y+30,30,32));
        Text(new Rect(r.x+56,r.y+13,r.width-65,19),caption,12,false,Muted);
        Text(new Rect(r.x+56,r.y+34,r.width-65,36),value,26);
    }
    private void ResultMetric(float x,Sprite icon,string caption,string value)
    {
        Icon(icon,new Rect(x+59,364,30,33));
        Text(new Rect(x,402,153,23),caption,12,true,Muted);
        Text(new Rect(x,433,153,38),value,29,true);
    }
    private void Control(float x,float y,string key,string caption,Sprite icon)
    {
        Icon(icon,new Rect(x,y+5,30,32));
        Text(new Rect(x+43,y,246,21),key,13,false,Green);
        Text(new Rect(x+43,y+24,246,24),caption,18);
    }
    private bool Button(Rect r,string caption,Sprite icon=null)
    {
        bool hover=r.Contains(Event.current.mousePosition), held=hover&&Input.GetMouseButton(0);
        NineSlice(held?buttonPressed:hover?buttonHover:buttonNormal,r,4,3);
        Icon(icon,new Rect(r.x+19,r.center.y-13,27,26));
        button.fontSize=r.height>=50?23:19;
        return GUI.Button(r,caption,button);
    }
    private void Panel(Rect r)
    {
        NineSlice(panelSprite,new Rect(r.x+4,r.y+6,r.width,r.height),5,3,new Color(0,0,0,.28f));
        NineSlice(panelSprite,r,5,3);
    }
    private static void Rule(float x,float y,float width) { Fill(new Rect(x,y,width,2),new Color(.55f,.4f,.22f,.4f)); }
    private void Text(Rect r,string text,int size,bool center=false,Color? color=null)
    {
        GUIStyle style=center?centered:label;
        style.fontSize=size; style.normal.textColor=color??Ink;
        GUI.Label(r,text,style);
    }
    private static string TimeText(float t)
    {
        int s=Mathf.Max(0,Mathf.FloorToInt(t));
        return (s/60).ToString("00")+":"+(s%60).ToString("00");
    }
    private static void Icon(Sprite sprite,Rect r)
    {
        if(sprite==null)return;
        float fit=Mathf.Min(r.width/sprite.rect.width,r.height/sprite.rect.height);
        Vector2 size=sprite.rect.size*fit;
        DrawSprite(sprite,new Rect(r.center.x-size.x/2,r.center.y-size.y/2,size.x,size.y));
    }
    private static void DrawSprite(Sprite sprite,Rect r)
    {
        if(sprite==null)return;
        Rect s=sprite.rect;
        GUI.DrawTextureWithTexCoords(r,sprite.texture,new Rect(s.x/sprite.texture.width,s.y/sprite.texture.height,s.width/sprite.texture.width,s.height/sprite.texture.height));
    }
    private static void NineSlice(Sprite sprite,Rect r,float border,float pixelScale,Color? tint=null)
    {
        if(sprite==null){Fill(r,new Color(.89f,.82f,.60f));return;}
        Color previous=GUI.color; GUI.color=tint??Color.white;
        Rect s=sprite.rect;
        border=Mathf.Min(border,Mathf.Min(s.width,s.height)/3);
        float corner=Mathf.Min(border*pixelScale,Mathf.Min(r.width,r.height)/2);
        for(int row=0;row<3;row++)
        for(int col=0;col<3;col++)
        {
            float dx=col==0?r.x:col==1?r.x+corner:r.xMax-corner;
            float dy=row==0?r.y:row==1?r.y+corner:r.yMax-corner;
            float dw=col==1?r.width-2*corner:corner, dh=row==1?r.height-2*corner:corner;
            float sx=col==0?s.x:col==1?s.x+border:s.xMax-border;
            float sy=row==0?s.yMax-border:row==1?s.y+border:s.y;
            float sw=col==1?s.width-2*border:border, sh=row==1?s.height-2*border:border;
            GUI.DrawTextureWithTexCoords(new Rect(dx,dy,dw,dh),sprite.texture,
                new Rect(sx/sprite.texture.width,sy/sprite.texture.height,sw/sprite.texture.width,sh/sprite.texture.height));
        }
        GUI.color=previous;
    }
    private static void Fill(Rect r,Color color)
    {
        Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;
    }
}
