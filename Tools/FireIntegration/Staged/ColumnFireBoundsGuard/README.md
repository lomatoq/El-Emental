# Exact decorative billboard bounds

CPU upload pads each axis by particle.Width*particle.Aspect. The preceding shader checked horizontal/vertical magnitudes separately but ignored their combined projection on a world axis. At aspect.85 and expansion1.12, the theoretical tongue corner maximum is1.045654 times the CPU padding. That can produce edge culling.

One shader overlay computes `.5*width*(abs(right)+abs(up)*aspect)` after role-dependent shape scaling, then uniformly fits width to99 percent of CPU padding only when necessary. This is an exact per-axis bound for all four corners, with a small floating-point margin. CPU/source profiles and effects budgets are unchanged; maximum reduction for the authored range is about5.3 percent, and ordinary views are unchanged.

The pure oracle checks600000corners: old maximum1.038113, guarded maximum.99; analytical old worst1.045654. Current actual Presentation and Authoring C# compilation exit0, with existing obsolete warnings. HLSL/Unity validation remains pending because root's Unity connection is unavailable. Import only this one file then refresh; no installer needed.
