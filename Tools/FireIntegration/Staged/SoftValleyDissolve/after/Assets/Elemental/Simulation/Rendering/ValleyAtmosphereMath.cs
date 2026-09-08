using System;
namespace Elemental.Simulation.Rendering
{
    // Integral of exp(-max(height,0)/falloff) along a finite world-metre ray.
    public static class ValleyAtmosphereMath
    {
        public static double IntegratedDensity(double startHeight,double endHeight,double distance,double falloff)
        {
            if(double.IsNaN(startHeight)||double.IsInfinity(startHeight)||double.IsNaN(endHeight)||double.IsInfinity(endHeight)||
               double.IsNaN(distance)||double.IsInfinity(distance)||distance<0||double.IsNaN(falloff)||double.IsInfinity(falloff)||falloff<=0)
                throw new ArgumentOutOfRangeException("Valley atmosphere requires finite heights, nonnegative ray length and positive falloff.");
            if(distance==0)return 0;
            if(startHeight<=0 && endHeight<=0)return distance;
            double lo=Math.Min(startHeight,endHeight),hi=Math.Max(startHeight,endHeight);
            if(lo>=0)return Above(lo,hi,distance,falloff);
            double below=distance*(-lo)/(hi-lo);
            return below+Above(0,hi,distance-below,falloff);
        }
        private static double Above(double lo,double hi,double length,double falloff)
        {
            double x=(hi-lo)/falloff;
            double ratio=x<0.001?1-x*0.5+x*x/6:(1-Math.Exp(-x))/x;
            return length*Math.Exp(-lo/falloff)*ratio;
        }
        public static double Opacity(double startHeight,double endHeight,double distance,double falloff,double density)
            =>1-Math.Exp(-Math.Max(0,density)*IntegratedDensity(startHeight,endHeight,distance,falloff));
        public static double SkyOpacity(double height,double upSlope,double falloff,double density)
        {
            if(density<=0)return 0;
            if(upSlope<=0)return 1;
            double integral=(Math.Max(-height,0)+falloff*Math.Exp(-Math.Max(height,0)/falloff))/upSlope;
            return 1-Math.Exp(-density*integral);
        }
        public static double CloudTransmittance(double startHeight,double endHeight,double distance,double falloff,double density)
            =>Math.Exp(-Math.Max(0,density)*IntegratedDensity(startHeight,endHeight,distance,falloff));
        private static double Smooth(double start,double end,double value)
        {double t=Math.Max(0,Math.Min(1,(value-start)/(end-start)));return t*t*(3-2*t);}
        public static double OpaqueProtection(double cameraDistance,double surfaceRadius,double planetRadius,double clearRange)
            =>Smooth(clearRange,clearRange+100,cameraDistance)*Smooth(planetRadius+80,planetRadius+140,surfaceRadius);
        public static double UpperWindowProtection(double cameraDistance,double surfaceRadius,double surfaceHeight,double planetRadius,double clearRange)
        {
            double upper=Smooth(planetRadius*0.45,planetRadius*0.75,surfaceHeight);
            return 1-upper*(1-OpaqueProtection(cameraDistance,surfaceRadius,planetRadius,clearRange));
        }
        public static double LowerTerrainOpacity(double surfaceHeight,double planetRadius)
            =>1-Smooth(-planetRadius*0.25,planetRadius*0.60,surfaceHeight);
        // Distant scenery uses only integrated fog; the explicit underside seal is planet-local.
        public static double ComposeSurfaceOpacity(double veil,double aerial,double surfaceHeight,double surfaceRadius,double planetRadius,double protection)
        {
            double planetMembership=1-Smooth(planetRadius+80,planetRadius+140,surfaceRadius);
            double lower=LowerTerrainOpacity(surfaceHeight,planetRadius)*planetMembership;
            return Math.Max(lower,1-(1-Math.Clamp(veil,0,1))*(1-Math.Clamp(aerial,0,1)))*Math.Clamp(protection,0,1);
        }
        public static double FarOpacity(double distance,double clearRange,double hazeDistance,double maximum)
            =>Math.Min(Math.Max(0,maximum),Math.Max(0,maximum)*(1-Math.Exp(-Math.Max(0,distance-clearRange)/Math.Max(1,hazeDistance))));
    }
}
