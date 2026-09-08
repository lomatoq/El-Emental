Warm ivory #FFF4DD, deep blue-black #081521, sandstone gold #F5D7A1, cyan #41CFFF, red #F25153. Existing HUD font scale +12% preserved. Menu: Don Graffiti title, Varose short English labels; readable existing font numbers/errors. Hover .10 s, press .07, release .12; .98 press scale.

### Assets/Elemental/Content/UI/EarthDuelHud.uss
```
.duel-hud { position: absolute; left: 0; right: 0; top: 0; bottom: 0; color: rgb(255, 244, 221); }
.duel-scoreboard { position: absolute; top: 20px; left: 50%; translate: -50% 0; width: 390px; height: 95px; flex-direction: row; justify-content: center; align-items: flex-start; }
.duel-team { width: 90px; align-items: center; border-bottom-width: 2px; padding-bottom: 8px; }
.duel-red { border-bottom-color: rgb(242, 81, 83); }
.duel-blue { border-bottom-color: rgb(65, 207, 255); }
.duel-score { font-size: 51.5px; -unity-font-style: bold; margin-top: -6px; margin-bottom: -6px; }
.duel-caption { font-size: 11.2px; letter-spacing: 2px; color: rgba(255, 237, 199, .8); -unity-text-align: middle-center; }
.duel-clock { width: 196px; height: 88px; align-items: center; padding-top: 9px; background-color: rgba(8, 21, 33, .65); border-left-width: 1px; border-right-width: 1px; border-bottom-width: 2px; border-top-width: 1px; border-left-color: rgb(244, 105, 101); border-right-color: rgb(65, 207, 255); border-bottom-color: rgb(245, 215, 161); border-top-color: rgba(245, 215, 161, .65); border-bottom-left-radius: 28px; border-bottom-right-radius: 28px; }
.duel-time { font-size: 38.1px; -unity-font-style: bold; letter-spacing: 2px; margin-top: 1px; }
.duel-side { position: absolute; top: 40%; width: 58px; align-items: center; }
.duel-left { left: 14px; }
.duel-right { right: 14px; }
.duel-gauge { width: 58px; height: 290px; }
.duel-symbol { font-size: 26.9px; -unity-font-style: bold; margin-top: -4px; -unity-text-align: middle-center; }
.duel-vital-value { font-size: 13.4px; letter-spacing: 1px; -unity-text-align: middle-center; color: rgba(255, 245, 223, .9); }
.duel-navigation { position: absolute; right: 27px; bottom: 22px; width: 218px; align-items: center; }
.duel-globe { width: 218px; height: 218px; }
.duel-legend { font-size: 10.1px; letter-spacing: 1px; color: rgba(255, 237, 199, .65); -unity-text-align: middle-center; }
.duel-respawn { position: absolute; bottom: 18%; left: 0; right: 0; -unity-text-align: middle-center; font-size: 17.9px; letter-spacing: 3px; }
.duel-result { position: absolute; left: 50%; top: 38%; translate: -50% 0; width: 360px; padding: 28px; align-items: center; background-color: rgba(8, 21, 33, .93); border-top-width: 2px; border-bottom-width: 1px; border-top-color: rgb(245, 215, 161); border-bottom-color: rgb(245, 215, 161); border-bottom-left-radius: 24px; border-bottom-right-radius: 24px; }
.duel-result-title { font-size: 37.0px; -unity-font-style: bold; margin-top: 10px; margin-bottom: 18px; }
.duel-restart { width: 200px; height: 43px; color: rgb(255, 244, 221); background-color: rgba(40, 93, 116, .9); border-width: 1px; border-color: rgb(94, 196, 221); border-radius: 3px; letter-spacing: 2px; }
.duel-restart:hover { background-color: rgb(49, 119, 144); }
.duel-restart:focus { border-color: rgb(255, 235, 182); }

```
