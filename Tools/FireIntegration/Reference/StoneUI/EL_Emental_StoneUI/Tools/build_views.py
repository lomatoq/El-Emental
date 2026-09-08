#!/usr/bin/env python3
"""Build UXML screen library, tokens, animation presets, and screen contract."""
from pathlib import Path
import json, html
R=Path(__file__).resolve().parents[1]; U=R/'Assets/ElEmentalStoneUI'; UI=U/'UI'; (UI/'Screens').mkdir(parents=True,exist_ok=True); (U/'Config').mkdir(exist_ok=True)
T={'version':'1.0.0','referenceResolution':[1920,1080],'spacing':[4,8,12,16,24,32,48,64],
'colors':{'ink':'#0B1417','surface':'#152022','gold':'#CCB77D','ivory':'#F2E6C7','muted':'#9DA8A7','earth':'#B9DB74','water':'#72C5E8','fire':'#F49A68','air':'#C3CDD0','danger':'#F17668','disabled':'#5C6767'},
'fonts':{'brand':'immutable wordmark_master texture','displayLatin':'Cinzel Regular / Semibold','displayCyrillic':'Forum Regular','body':'Manrope Regular / Medium','numbers':'Manrope Semibold; reserve fixed digit widths'},
'fontSizes':{'result':112,'screenTitle':30,'button':29,'body':21,'hint':17,'micro':15,'timer':48,'meter':30},
'layout':{'curtain':[0,0,784,1080],'brand':[87,150,460,112],'menuContent':[87,380,570,602],'settingsContent':[87,324,570,674],'hudSafeInset':24,'minButtonHeight':56,'minimumContrastBody':4.5},
'implementation':{'renderer':'UI Toolkit','panelScaleMode':'ScaleWithScreenSize','screenMatchMode':'MatchWidthOrHeight','match':1,'sortingOrder':100,'doNotChange':['physics','network protocol','planet radius','live camera','live render pipeline']}}
(U/'Config/design_tokens.json').write_text(json.dumps(T,indent=2,ensure_ascii=False))
PRESETS=[
('panel_enter',.28,'outCubic',{'x':-28,'alpha':0},{'x':0,'alpha':1},False),('panel_exit',.16,'inQuad',{'x':0,'alpha':1},{'x':-16,'alpha':0},False),
('content_enter',.20,'outCubic',{'y':8,'alpha':0},{'y':0,'alpha':1},False),('button_hover',.13,'outQuad',{'scale':1,'glow':0},{'scale':1.014,'glow':.42},False),
('button_press',.07,'outQuad',{'scale':1},{'scale':.984},False),('button_release',.12,'outCubic',{'scale':.984},{'scale':1},False),
('focus_enter',.10,'linear',{'focus':0},{'focus':1},False),('element_select',.22,'outCubic',{'scale':.92,'glow':0},{'scale':1,'glow':.7},False),
('selected_breath',3.6,'sine',{'opacity':.28},{'opacity':.42},True),('success',.48,'outCubic',{'y':12,'scale':.96,'alpha':0},{'y':0,'scale':1,'alpha':1},False),
('defeat',.34,'outCubic',{'y':5,'alpha':0},{'y':0,'alpha':1},False),('toast_in',.16,'outCubic',{'y':8,'alpha':0},{'y':0,'alpha':1},False),
('toast_out',.16,'inQuad',{'alpha':1},{'alpha':0},False),('meter_value',.14,'outQuad',{'value':'current'},{'value':'target'},False),
('meter_damage_tail',.36,'outQuad',{'value':'previous'},{'value':'target'},False),('error_feedback',.16,'sine',{'x':0},{'x':[0,-3,3,-2,0]},False),
('shine_pass',.6,'linear',{'phase':0},{'phase':1},False),('connect_pulse',1.8,'sine',{'opacity':.35},{'opacity':.75},True),
('menu_camera_blend',.85,'smoothstep',{'pose':'existing gameplay'},{'pose':'menu camera marker'},False),('island_float',110,'sine',{'offset':-1.2},{'offset':1.2},True)]
P={'version':1,'clock':'unscaled UI time; world motion policy selectable','reduced_motion':{'translation':0,'scale':1,'looping':False,'fadeMaxSeconds':.08,'cameraMotion':False},'presets':[dict(id=i,duration=d,ease=e,start=a,end=b,loop=l)for i,d,e,a,b,l in PRESETS]}
(U/'Config/animation_presets.json').write_text(json.dumps(P,indent=2))

def ve(name='',classes='',body='',extra=''):
 return f'<ui:VisualElement name="{name}" class="{classes}" {extra}>{body}</ui:VisualElement>'
def label(text,classes='',name=''):
 return f'<ui:Label name="{name}" text="{html.escape(text,quote=True)}" class="{classes}" />'
def icon(id,classes=''):
 return ve('',f'art art-{id} {classes}')
def btn(id,title,glyph='icon_next',sub='',primary=False,small=False):
 body=icon('button_halo','button-glow')+ve('','button-surface')+icon(glyph,'button-icon')+ve('','button-labels',label(title,'button-label')+(label(sub,'button-sub')if sub else ''))+icon('icon_next','button-arrow')+ve('','button-focus')
 return f'<ui:Button name="{id}" class="ee-button {"primary"if primary else ""} {"small"if small else ""}">{body}</ui:Button>'
def back():return btn('back','BACK','icon_back',small=True)
def brand(small=False):
 return ve('brand','brand'+(' brand-small'if small else ''),icon('emblem_master','brand-emblem')+label('LOCAL ALPHA / 01','brand-build')+icon('wordmark_master','wordmark')+label('SHAPE THE WORLD.\nHOLD YOUR GROUND.','tagline'))
def shell(id,title,body,settings=False):
 return ve('screen-root','ee-screen menu-screen',ve('demo-background','demo-background')+ve('safe-root','safe-root',icon('curtain_left','panel-art')+brand()+label(title,'screen-title')+f'<ui:ScrollView name="content" class="menu-content {"settings-content" if settings else ""}" horizontal-scroller-visibility="Hidden">{body}</ui:ScrollView>'+label('','status-label','status-label')+label('UI PREVIEW — NO GAME / NETWORK','demo-badge','demo-badge')))
def result(id,title,sub,won=True):
 return ve('screen-root',f'ee-screen result-screen {"win"if won else "loss"}',ve('demo-background','demo-background')+ve('scrim','result-scrim')+ve('safe-root','safe-root',brand(True)+ve('result-content','result-content',icon('glow_soft','result-halo')+icon('element_earth','result-emblem')+label(title,'result-title','result-title')+icon('divider','result-divider')+label(sub,'result-score','result-score')+btn('rematch','REMATCH'if won else 'RETRY','icon_refresh',primary=True)+btn('menu','BACK TO MENU','icon_back',small=True))+label('UI PREVIEW — NO GAME / NETWORK','demo-badge','demo-badge')))
S={}
S['MainMenu']=shell('MainMenu','SELECT MODE',btn('bot','PLAY VS BOT','icon_bot','Challenge an AI',True)+btn('practice','PRACTICE','element_earth','Learn and improve')+btn('multiplayer','MULTIPLAYER','icon_players','Host or join by code')+ve('','footer-links',f'<ui:Button name="settings" text="SETTINGS" class="text-button"/><ui:Button name="exit" text="EXIT" class="text-button"/>'))
S['ModeSelect']=shell('ModeSelect','SELECT MODE',btn('practice','PRACTICE','element_earth','Learn and improve',True)+btn('bot','VS BOT','icon_swords','Challenge an AI')+btn('multiplayer','MULTIPLAYER','icon_players','Host or join by code')+back())
S['Multiplayer']=shell('Multiplayer','MULTIPLAYER',label('One duel. One code.','intro')+ve('','choice-row',btn('host','HOST','icon_host',primary=True)+btn('guest','GUEST','icon_join'))+label('Create a room or enter a friend’s code.','body-copy')+back())
S['Host']=shell('Host','HOST A GAME',label('ROOM CODE','overline')+ve('','code-display',label('—','room-code','room-code')+f'<ui:Button name="copy" text="COPY" class="text-button"/>')+ve('','connection-line',icon('icon_network','status-icon')+label('Waiting for room service…','body-copy','connection-status'))+label('1 / 2','occupancy','occupancy')+label('The duel starts when the second player connects.','body-copy')+back())
S['Guest']=shell('Guest','JOIN A GAME',label('Enter the complete room code.','intro')+'<ui:TextField name="room-input" label="ROOM CODE" class="code-input" max-length="64" />'+label('','validation','validation')+btn('join','JOIN','icon_join',primary=True)+back())
S['Connecting']=shell('Connecting','CONNECTING',icon('icon_network','large-status-icon')+label('Connecting to the room…','intro','connection-status')+label('Your request is handled by the existing network service.','body-copy')+btn('cancel','CANCEL','icon_close',small=True))
elements=ve('','element-row',''.join(f'<ui:Button name="element-{e}" class="element-tab {"selected"if e=="earth"else "locked"}">{icon("element_"+e)}{label(e.upper(),"element-label")}</ui:Button>'for e in ['fire','earth','water','air']))
sliders=''
for id,title,val in [('master','MASTER VOLUME',80),('ui','UI VOLUME',50),('sensitivity','CAMERA SENSITIVITY',45)]:
 sliders+=ve('', 'setting-row', f'<ui:Slider name="{id}" label="{title}" low-value="0" high-value="100" value="{val}" class="ee-slider" />'+label(str(val),'setting-value',id+'-value'))
S['Settings']=shell('Settings','',elements+ve('','element-ribbon',icon('element_earth','ribbon-icon')+label('EARTH ELEMENT ACTIVE','ribbon-label','active-element-label'))+sliders+'<ui:Toggle name="reduced-motion" label="REDUCED MOTION" class="ee-toggle" />'+back()+label('Settings are saved automatically.','small-copy'),True)
S['Pause']=shell('Pause','PAUSED',btn('resume','RESUME','icon_play',primary=True)+btn('settings','SETTINGS','icon_settings')+btn('controls','CONTROLS','icon_keyboard')+btn('menu','BACK TO MENU','icon_back',small=True))
S['ConnectionError']=shell('ConnectionError','CONNECTION LOST',icon('icon_warning','large-status-icon')+label('The connection was interrupted.','intro','error-message')+btn('retry-connect','TRY AGAIN','icon_refresh',primary=True)+btn('menu','BACK TO MENU','icon_back',small=True))
S['ConfirmExit']=shell('ConfirmExit','LEAVE THE GAME?',label('Your current duel will be left only after confirmation.','intro')+btn('confirm-exit','LEAVE','icon_exit',primary=True)+btn('back','STAY','icon_back',small=True))
S['Loading']=shell('Loading','LOADING',label('Preparing the arena…','intro','loading-label')+ve('','loading-track',ve('loading-fill','loading-fill'))+label('','small-copy','loading-progress'))
S['Controls']=shell('Controls','CONTROLS',label('Controls come from the current input bindings.','intro')+label('Bind the existing input-action display strings here.\nDo not hard-code a second set of combat controls.','body-copy','controls-text')+back())
S['Victory']=result('Victory','VICTORY','3 — 1',True); S['Defeat']=result('Defeat','DEFEAT','1 — 3',False);S['Draw']=result('Draw','DRAW','2 — 2',True)

def meter(side,title,val):
 return ve(title.lower()+'-meter','meter meter-'+side,icon('meter_frame_'+side,'meter-frame')+ve(title.lower()+'-clip','meter-clip',icon('meter_fill_'+side,'meter-fill '+title.lower()+'-fill'))+icon('icon_health'if title=='HP'else 'icon_mana','meter-symbol')+label(str(val),'meter-number',title.lower()+'-value')+label(title,'meter-name'))
score=ve('','scoreboard',label('0','team-score','red-score')+icon('team_wing','team-wing red-wing')+ve('','timer-wrap',icon('timer_plate','timer-art')+label('EARTH DUEL','timer-caption','mode-label')+label('04:56','timer','timer-value'))+icon('team_wing','team-wing blue-wing')+label('0','team-score','blue-score'))
wheel=ve('','element-wheel',icon('circle_ring','wheel-ring')+''.join(ve('','wheel-token token-'+e,icon('circle_frame','token-frame')+icon('element_'+e,'token-glyph')+label(e.upper(),'token-label'))for e in ['fire','water','air','earth']))
mini=ve('minimap','minimap',icon('minimap_frame','minimap-art')+ve('minimap-data','minimap-data')+icon('icon_arrow_player','player-arrow')+label('N','map-north')+label('LOCAL DRIFT','map-caption'))
S['HUD']=ve('screen-root','ee-screen hud-screen',ve('demo-background','demo-background')+ve('safe-root','safe-root',icon('wordmark_master','hud-wordmark')+score+f'<ui:Button name="pause" class="pause-button">{icon("icon_pause")}</ui:Button>'+meter('left','HP',78)+meter('right','MP',100)+wheel+mini+label('','game-status','game-status')+label('UI PREVIEW — NO GAME / NETWORK','demo-badge','demo-badge')))

def write_uxml(name,body):
 txt='<?xml version="1.0" encoding="utf-8"?>\n<ui:UXML xmlns:ui="UnityEngine.UIElements">\n <Style src="../StoneTheme.uss"/>\n'+body+'\n</ui:UXML>'
 (UI/'Screens'/f'{name}.uxml').write_text(txt)
for k,v in S.items():write_uxml(k,v)
# Source-of-truth screen structure read by both the runtime and installation tools.
contracts=[]
import re
for name,body in S.items():
 contracts.append({'id':name,'uxml':f'UI/Screens/{name}.uxml','layout':'hud'if name=='HUD'else 'result'if name in ('Victory','Defeat','Draw') else 'left-curtain','blocksGameplayInput':name!='HUD','nodes':re.findall(r'name="([^"]+)"',body),'buttons':re.findall(r'<ui:Button name="([^"]+)"',body),'logo':'wordmark_master','priority':'core'if name not in ('ModeSelect','Controls','Draw')else 'support'})
(U/'Config/screen_contract.json').write_text(json.dumps({'schema':1,'screens':contracts},indent=2))

css='''/* EL EMENTAL / StoneUI v1 — UI Toolkit. No CSS-only web properties. */
.ee-screen { position:absolute; left:0; top:0; right:0; bottom:0; color:#F2E6C7; font-size:21px; }
.safe-root { position:absolute; left:0; top:0; right:0; bottom:0; }
.demo-background { position:absolute; left:0; top:0; right:0; bottom:0; -unity-background-scale-mode:scale-and-crop; display:none; }
.art { -unity-background-scale-mode:scale-to-fit; flex-shrink:0; }
.panel-art { position:absolute; left:0; top:0; width:784px; height:1080px; }
.brand { position:absolute; left:86px; top:60px; width:480px; height:272px; }
.brand-emblem { position:absolute; left:20px; top:0; width:76px; height:84px; }
.brand-build { position:absolute; left:118px; top:31px; font-size:16px; letter-spacing:2px; color:#DFD1AE; }
.wordmark { position:absolute; left:0; top:102px; width:460px; height:113px; }
.tagline { position:absolute; left:49px; top:225px; width:390px; -unity-text-align:middle-center; white-space:normal; font-size:16px; letter-spacing:3px; }
.screen-title { position:absolute; left:87px; top:337px; width:570px; font-size:29px; letter-spacing:4px; -unity-text-align:middle-center; }
.menu-content { position:absolute; left:87px; top:392px; width:570px; bottom:105px; }
.menu-content > .unity-scroll-view__content-viewport { overflow:hidden; }
.menu-content .unity-scroll-view__content-container { padding-right:5px; }
.settings-content { top:326px; bottom:48px; }
.ee-button { position:relative; height:114px; min-height:64px; flex-shrink:0; margin:0 0 18px 0; padding:0; border-width:0; border-radius:0; background-color:rgba(0,0,0,0); color:#F2E6C7; }
.ee-button.small { height:87px; margin-top:15px; }
.button-surface { position:absolute; left:0; top:0; right:0; bottom:0; background-image:url("../Art/Buttons/button_normal.png"); -unity-slice-left:64; -unity-slice-right:64; -unity-slice-top:25; -unity-slice-bottom:25; }
.primary .button-surface { background-image:url("../Art/Buttons/button_earth.png"); }
.ee-button:hover .button-surface, .ee-button:focus .button-surface { background-image:url("../Art/Buttons/button_hover.png"); }
.ee-button:active .button-surface { background-image:url("../Art/Buttons/button_pressed.png"); }
.ee-button:disabled .button-surface { background-image:url("../Art/Buttons/button_disabled.png"); }
.ee-button:disabled { color:#8B9594; }
.button-glow { position:absolute; left:-16px; right:-16px; top:-19px; bottom:-19px; opacity:0; -unity-background-image-tint-color:#B9DB74; transition-property:opacity; transition-duration:0.13s; }
.ee-button:hover .button-glow, .ee-button:focus .button-glow { opacity:0.6; }
.button-icon { position:absolute; left:33px; top:20px; width:73px; height:73px; }
.small .button-icon { top:20px; left:36px; width:47px; height:47px; }
.button-labels { position:absolute; left:142px; right:65px; top:0; bottom:0; justify-content:center; }
.button-label { font-size:29px; letter-spacing:3px; white-space:normal; }
.button-sub { margin-top:5px; font-size:19px; letter-spacing:1px; color:#C1C8BC; }
.small .button-label { font-size:23px; }
.button-arrow { position:absolute; right:31px; top:42px; width:28px; height:28px; opacity:0.7; }
.small .button-arrow { top:31px; }
.button-focus { position:absolute; left:10px; top:47px; width:12px; height:12px; background-color:#F2E6C7; rotate:45deg; opacity:0; }
.ee-button:focus .button-focus { opacity:1; }
.footer-links { flex-direction:row; justify-content:space-between; margin-top:10px; }
.text-button { min-height:48px; padding:8px 20px; background-color:rgba(0,0,0,0); border-width:0; color:#DFD2B0; font-size:18px; letter-spacing:2px; }
.text-button:hover, .text-button:focus { color:#F2E6C7; background-color:rgba(185,219,116,0.1); }
.status-label { position:absolute; left:87px; bottom:58px; width:570px; font-size:16px; color:#CCB77D; white-space:normal; }
.demo-badge { position:absolute; right:24px; bottom:14px; font-size:13px; color:#E4E3CC; background-color:rgba(6,15,20,0.7); padding:4px 8px; letter-spacing:1px; }
.intro { font-size:24px; margin:10px 0 28px; white-space:normal; color:#DFDCCB; }
.body-copy { white-space:normal; font-size:21px; color:#BEC5BF; margin:18px 0 28px; }
.small-copy { font-size:16px; white-space:normal; color:#9DA8A7; margin-top:5px; }
.overline { font-size:17px; letter-spacing:3px; color:#CCB77D; margin-top:16px; }
.choice-row { flex-direction:row; justify-content:space-between; }
.choice-row .ee-button { width:49%; height:176px; }
.choice-row .button-icon { left:95px; top:23px; width:66px; height:66px; }
.choice-row .button-labels { left:15px; right:15px; top:97px; bottom:15px; }
.choice-row .button-label { -unity-text-align:middle-center; }
.choice-row .button-arrow { display:none; }
.code-display { flex-direction:row; align-items:center; justify-content:space-between; height:122px; background-image:url("../Art/Panels/code_field.png"); -unity-slice-left:30; -unity-slice-right:30; -unity-slice-top:30; -unity-slice-bottom:30; padding:20px; margin-top:12px; }
.room-code { font-size:42px; letter-spacing:6px; }
.connection-line { flex-direction:row; align-items:center; margin-top:25px; }
.status-icon { width:34px; height:34px; margin-right:16px; }
.occupancy { font-size:42px; margin-top:15px; }
.large-status-icon { width:96px; height:96px; margin:8px 0 20px; }
.code-input { flex-direction:column; margin:10px 0 20px; }
.code-input > .unity-base-field__label { font-size:17px; letter-spacing:3px; margin-bottom:16px; }
.code-input .unity-base-text-field__input { height:100px; font-size:38px; letter-spacing:5px; background-color:#152022; border-color:#CCB77D; border-width:1px; padding:15px 24px; color:#F2E6C7; }
.validation { min-height:35px; color:#F17668; font-size:18px; white-space:normal; }
.element-row { flex-direction:row; justify-content:space-between; height:88px; margin-bottom:13px; }
.element-tab { position:relative; width:111px; height:85px; padding:0; margin:0; border-width:0; background-color:rgba(0,0,0,0); }
.element-tab .art { width:51px; height:51px; align-self:center; }
.element-label { -unity-text-align:middle-center; font-size:13px; letter-spacing:2px; margin-top:8px; color:#AEB6AA; }
.element-tab.selected .element-label { color:#B9DB74; }
.element-tab.locked { opacity:0.46; }
.element-ribbon { height:72px; flex-direction:row; align-items:center; background-image:url("../Art/Panels/ribbon.png"); -unity-slice-left:46; -unity-slice-right:46; -unity-slice-top:30; -unity-slice-bottom:30; margin-bottom:21px; }
.ribbon-icon { width:60px; height:60px; margin:0 21px; }
.ribbon-label { color:#B9DB74; font-size:19px; letter-spacing:1px; }
.setting-row { height:93px; position:relative; }
.ee-slider { flex-direction:column; margin:0; padding-right:60px; }
.ee-slider > .unity-base-field__label { font-size:18px; letter-spacing:2px; margin-bottom:15px; }
.ee-slider .unity-base-slider__tracker { height:3px; background-color:#7E806B; }
.ee-slider .unity-base-slider__dragger { width:24px; height:24px; margin-top:-10px; border-radius:0; border-width:2px; border-color:#F2E6C7; background-color:#BDAB72; rotate:45deg; }
.setting-value { position:absolute; right:5px; top:31px; font-size:24px; }
.ee-toggle { min-height:52px; margin-top:6px; font-size:18px; letter-spacing:2px; }
.ee-toggle .unity-toggle__checkmark { width:27px; height:27px; border-color:#CCB77D; border-width:2px; background-color:#10191C; }
.loading-track { height:5px; margin:20px 0; background-color:#293334; }
.loading-fill { height:5px; width:0%; background-color:#B9DB74; }
.result-scrim { position:absolute; left:0; top:0; right:0; bottom:0; background-color:rgba(5,13,16,0.6); }
.loss .result-scrim { background-color:rgba(25,8,4,0.78); }
.brand-small { left:50%; margin-left:-160px; top:34px; width:320px; height:145px; }
.brand-small .brand-emblem, .brand-small .brand-build { display:none; }
.brand-small .wordmark { left:0; top:0; width:320px; height:82px; }
.brand-small .tagline { top:90px; left:0; width:320px; font-size:13px; letter-spacing:2px; }
.result-content { position:absolute; left:50%; top:226px; width:650px; margin-left:-325px; align-items:center; }
.result-halo { position:absolute; width:500px; height:500px; left:75px; top:-70px; opacity:0.28; -unity-background-image-tint-color:#B9DB74; }
.result-emblem { width:176px; height:176px; }
.result-title { font-size:112px; letter-spacing:6px; color:#F2E6C7; margin-top:-3px; }
.result-divider { width:570px; height:40px; }
.result-score { font-size:58px; margin:0 0 22px; letter-spacing:8px; }
.result-content .ee-button { width:580px; height:104px; }
.result-content .small { width:530px; height:82px; margin-top:0; }
.loss .result-halo { -unity-background-image-tint-color:#F17668; }
.loss .result-title { color:#F7B996; }
.loss .primary .button-surface { background-image:url("../Art/Buttons/button_danger.png"); }
.hud-wordmark { position:absolute; left:28px; top:23px; width:276px; height:72px; }
.scoreboard { position:absolute; top:17px; left:50%; margin-left:-310px; width:620px; height:122px; flex-direction:row; align-items:center; justify-content:center; }
.team-score { width:88px; font-size:46px; -unity-text-align:middle-center; }
.team-wing { width:71px; height:35px; }
.red-wing { -unity-background-image-tint-color:#F17668; }
.blue-wing { -unity-background-image-tint-color:#72C5E8; scale:-1 1; }
.timer-wrap { width:278px; height:117px; position:relative; }
.timer-art { position:absolute; left:0; top:0; right:0; bottom:0; }
.timer-caption { position:absolute; left:0; top:21px; width:100%; -unity-text-align:middle-center; font-size:14px; letter-spacing:3px; }
.timer { position:absolute; left:0; top:44px; width:100%; -unity-text-align:middle-center; font-size:45px; letter-spacing:6px; }
.pause-button { position:absolute; right:29px; top:28px; width:62px; height:62px; border-width:1px; border-color:#CCB77D; background-color:rgba(12,22,27,0.75); }
.pause-button .art { width:32px; height:32px; margin:8px; }
.meter { position:absolute; top:35%; width:102px; height:400px; }
.meter-left { left:0; } .meter-right { right:0; }
.meter-frame { position:absolute; left:0; top:0; width:102px; height:318px; }
.meter-clip { position:absolute; left:0; bottom:82px; width:102px; height:318px; overflow:hidden; }
.meter-fill { position:absolute; left:0; bottom:0; width:102px; height:318px; }
.hp-fill { -unity-background-image-tint-color:#F17668; } .mp-fill { -unity-background-image-tint-color:#72C5E8; }
.meter-symbol { position:absolute; left:34px; top:324px; width:26px; height:26px; }
.meter-number { position:absolute; top:354px; width:100%; -unity-text-align:middle-center; font-size:30px; }
.meter-name { position:absolute; top:390px; width:100%; -unity-text-align:middle-center; font-size:13px; letter-spacing:3px; }
.element-wheel { position:absolute; bottom:12px; left:50%; margin-left:-160px; width:320px; height:157px; }
.wheel-ring { position:absolute; width:116px; height:116px; left:102px; top:0; opacity:0.55; }
.wheel-token { position:absolute; width:40px; height:40px; }
.token-frame { position:absolute; left:0; top:0; right:0; bottom:0; }
.token-glyph { position:absolute; left:7px; top:7px; right:7px; bottom:7px; }
.token-fire { left:86px; top:39px; } .token-air { left:194px; top:39px; } .token-water { left:140px; top:-15px; }
.token-earth { left:124px; top:77px; width:72px; height:72px; }
.token-earth .token-glyph { left:11px; top:11px; right:11px; bottom:11px; }
.token-label { position:absolute; top:100%; width:76px; left:-18px; font-size:11px; letter-spacing:2px; -unity-text-align:middle-center; }
.token-earth .token-label { width:100px; left:-14px; color:#D6EBAA; font-size:13px; }
.minimap { position:absolute; right:27px; bottom:40px; width:238px; height:238px; }
.minimap-art { position:absolute; left:0; top:0; width:238px; height:238px; }
.minimap-data { position:absolute; left:27px; top:27px; width:184px; height:184px; overflow:hidden; border-radius:92px; opacity:0.8; }
.player-arrow { position:absolute; left:104px; top:104px; width:30px; height:30px; -unity-background-image-tint-color:#B9DB74; }
.map-north { position:absolute; top:18px; width:100%; -unity-text-align:middle-center; font-size:12px; }
.map-caption { position:absolute; top:230px; width:100%; -unity-text-align:middle-center; font-size:13px; letter-spacing:3px; }
.game-status { position:absolute; left:50%; bottom:184px; width:500px; margin-left:-250px; -unity-text-align:middle-center; font-size:18px; }
.reduced-motion .button-glow { transition-duration:0.08s; }
'''
# Map all art class selectors to one referenced texture. Relative assets survive project moves.
manifest=json.loads((R/'Source/asset_manifest.json').read_text())
for a in manifest['assets']:
 rel='../'+a['file'].split('Assets/ElEmentalStoneUI/')[1]
 css+=f'\n.art-{a["id"]} {{ background-image:url("{rel}"); }}'
(UI/'StoneTheme.uss').write_text(css)
print('Built',len(S),'UXML screens, token system and',len(PRESETS),'animation presets')
