from pathlib import Path
import shutil
lane=Path(__file__).resolve().parent;root=lane.parents[3]
def edit(rel,fn):
 p=root/rel;a=lane/'after'/rel;b=lane/'before'/rel;a.parent.mkdir(parents=True,exist_ok=True);b.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,b);a.write_text(fn(p.read_text(encoding='utf-8-sig')),encoding='utf-8')
edit('Assets/Elemental/Content/Shaders/ValleyAtmosphereV2.hlsl',lambda s:s.replace('float lowerTerrain=1-smoothstep(-radius*0.25,radius*0.60,surfaceLocal.y);\n        alpha=max(lowerTerrain,min(_ElementalValleyFar.z,1-(1-veil)*(1-aerial)))*cloudProtection;', '''// The forced underside seal belongs only to the planet. Applying it to
        // all distant columns painted a shared narrow horizontal opacity band.
        float lowerTerrain=(1-smoothstep(-radius*0.25,radius*0.60,surfaceLocal.y))*(1-planetProtect);
        // Only aerial haze is capped. Physical height fog must reach full opacity
        // continuously below the sea instead of stopping at the aerial cap.
        alpha=max(lowerTerrain,1-(1-veil)*(1-aerial))*cloudProtection;'''))
edit('Assets/Elemental/Content/Profiles/ValleyAtmosphereV2.asset',lambda s:s.replace('HeightFalloff: 45','HeightFalloff: 75').replace('VeilDensity: 0.018','VeilDensity: 0.012').replace('DayFog: {r: 0.78, g: 0.88, b: 0.98, a: 1}','DayFog: {r: 0.75, g: 0.865, b: 0.98, a: 1}'))
edit('Assets/Elemental/Simulation/Rendering/ValleyAtmosphereMath.cs',lambda s:s.replace('        public static double FarOpacity(','''        // Distant scenery uses only integrated fog; the explicit underside seal is planet-local.
        public static double ComposeSurfaceOpacity(double veil,double aerial,double surfaceHeight,double surfaceRadius,double planetRadius,double protection)
        {
            double planetMembership=1-Smooth(planetRadius+80,planetRadius+140,surfaceRadius);
            double lower=LowerTerrainOpacity(surfaceHeight,planetRadius)*planetMembership;
            return Math.Max(lower,1-(1-Math.Clamp(veil,0,1))*(1-Math.Clamp(aerial,0,1)))*Math.Clamp(protection,0,1);
        }
        public static double FarOpacity('''))
shutil.copy2(root/'Tools/FireIntegration/Staged/ProceduralCloudBanks/compile.py',lane/'compile.py')
