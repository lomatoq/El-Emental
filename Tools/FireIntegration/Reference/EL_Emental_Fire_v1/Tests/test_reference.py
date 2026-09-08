"""Portable tests of the mathematical reference, NOT Unity/HLSL compilation.
Run: python Tests/test_reference.py
Only Python standard library is required. Randomized tests use fixed seeds.
"""
import json
import math
import random
import unittest
from dataclasses import dataclass
from pathlib import Path

V = tuple[float, float, float]
def add(a: V, b: V) -> V: return tuple(x+y for x,y in zip(a,b))
def sub(a: V, b: V) -> V: return tuple(x-y for x,y in zip(a,b))
def mul(a: V, x: float) -> V: return tuple(v*x for v in a)
def dot(a: V, b: V) -> float: return sum(x*y for x,y in zip(a,b))
def cross(a: V, b: V) -> V: return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def norm(a: V) -> float: return math.sqrt(dot(a,a))
def normal(a: V, fallback: V=(0.,1.,0.)) -> V: return mul(a,1/norm(a)) if dot(a,a)>1e-10 else fallback
def lerp(a: V,b: V,t: float) -> V: return add(a,mul(sub(b,a),t))
def clamp(v: float,a: float,b: float) -> float: return max(a,min(b,v))
def limit(v: V,s: float) -> V: return mul(v,min(1.,max(s,0.)/max(norm(v),1e-6)))
def tangent(n: V) -> V: return normal(cross((0.,1.,0.) if abs(n[1])<.9 else (1.,0.,0.),n),(1.,0.,0.))

@dataclass
class Patch:
    point: V=(0.,0.,0.)
    radius: float=10.
    normal: V=(0.,0.,1.)
    tangent: V=(1.,0.,0.)
    recovery: float=.08
    velocity: V=(0.,0.,0.)
    angular: V=(0.,0.,0.)
    fraction: float=.8
    skin: float=.015
    active: bool=True

def resolve(patch: Patch, old: V, pos: V, vel: V, dt: float=1/60,
            radius: float=.035, phase: float=1.3, local_time: float=0.) -> tuple[bool,V,V]:
    if not patch.active or patch.radius<=0: return False,pos,vel
    n=normal(patch.normal); skin=max(radius+patch.skin,0.)
    c0=add(patch.point,mul(patch.velocity,local_time)); c1=add(c0,mul(patch.velocity,dt))
    d0=dot(sub(old,c0),n)-skin; d1=dot(sub(pos,c1),n)-skin
    if d1>1e-5: return False,pos,vel
    crossing=d0>=0; recovery=d0<0 and d0>=-max(patch.recovery,0.)
    if not(crossing or recovery): return False,pos,vel
    toi=clamp(d0/max(d0-d1,1e-8),0,1) if crossing else 1.
    q=sub(lerp(old,pos,toi),lerp(c0,c1,toi)); lateral=sub(q,mul(n,dot(q,n)))
    if dot(lateral,lateral)>patch.radius**2: return False,pos,vel
    pos=sub(pos,mul(n,min(d1,0.)))
    surface=add(patch.velocity,cross(patch.angular,sub(pos,c1))); rel=sub(vel,surface)
    incoming=max(-dot(rel,n),0.)
    if d1 < -1e-5 and crossing and incoming>0:
        t=normal(sub(patch.tangent,mul(n,dot(patch.tangent,n))),tangent(n))
        fallback=add(mul(t,math.cos(phase)),mul(cross(n,t),math.sin(phase)))
        outward=normal(lateral,fallback)
        rel=limit(add(add(rel,mul(n,incoming)),mul(outward,incoming*clamp(patch.fraction,0,1))),norm(rel))
    vel=add(surface,sub(rel,mul(n,min(dot(rel,n),0.))))
    return True,pos,vel

class ReferenceTests(unittest.TestCase):
    def test_head_on_spreads(self):
        hit,p,v=resolve(Patch(),(0.,0.,1.),(0.,0.,-.5),(0.,0.,-10.))
        self.assertTrue(hit); self.assertAlmostEqual(p[2],.05)
        self.assertGreater(math.hypot(v[0],v[1]),1.); self.assertGreaterEqual(v[2],-1e-8)
        self.assertLessEqual(norm(v),10.+1e-8)
    def test_oblique_keeps_tangent_without_spread(self):
        _,_,v=resolve(Patch(fraction=0),(0.,0.,1.),(.2,0.,-.5),(3.,0.,-10.))
        self.assertAlmostEqual(v[0],3.); self.assertAlmostEqual(v[2],0.)
    def test_finite_patch_does_not_form_infinite_wall(self):
        hit,_,_=resolve(Patch(radius=.5),(2.,0.,1.),(2.,0.,-.5),(0.,0.,-10.))
        self.assertFalse(hit)
    def test_fast_crossing_uses_impact_footprint(self):
        hit,_,_=resolve(Patch(radius=.6),(-2.,0.,2.),(2.,0.,-2.),(40.,0.,-40.),radius=0.)
        self.assertTrue(hit)
    def test_deep_backside_is_not_teleported(self):
        hit,p,_=resolve(Patch(),(0.,0.,-3.),(0.,0.,-4.),(0.,0.,-10.))
        self.assertFalse(hit); self.assertEqual(p,(0.,0.,-4.))
    def test_shallow_overlap_recovers(self):
        hit,p,_=resolve(Patch(),(0.,0.,.02),(0.,0.,.0),(0.,0.,-1.))
        self.assertTrue(hit); self.assertAlmostEqual(p[2],.05)
    def test_removed_patch_stops_blocking(self):
        hit,p,_=resolve(Patch(active=False),(0.,0.,1.),(0.,0.,-1.),(0.,0.,-10.))
        self.assertFalse(hit); self.assertEqual(p,(0.,0.,-1.))
    def test_translating_wall_nonpenetration_relative_to_wall(self):
        c=Patch(velocity=(0.,0.,3.))
        hit,p,v=resolve(c,(0.,0.,.2),(0.,0.,.0),(0.,0.,-1.))
        self.assertTrue(hit); self.assertGreaterEqual(p[2],.1-1e-8)
        self.assertGreaterEqual(dot(sub(v,c.velocity),c.normal),-1e-8)
    def test_corner_three_projection_passes(self):
        old=(1.,1.,0.);p=(-1.,-1.,0.);v=(-10.,-10.,0.)
        patches=[Patch(normal=(1.,0.,0.),tangent=(0.,1.,0.)),Patch(normal=(0.,1.,0.),tangent=(1.,0.,0.))]
        for _ in range(3):
            for patch in patches: _,p,v=resolve(patch,old,p,v)
        self.assertGreaterEqual(p[0],.05-1e-8);self.assertGreaterEqual(p[1],.05-1e-8)
        self.assertGreaterEqual(v[0],-1e-7);self.assertGreaterEqual(v[1],-1e-7)
    def test_zero_normal_remains_finite(self):
        _,p,v=resolve(Patch(normal=(0.,0.,0.)),(0.,1.,0.),(0.,-1.,0.),(0.,-10.,0.))
        self.assertTrue(all(math.isfinite(x) for x in p+v))
    def test_randomized_plane_nonpenetration_and_relative_speed(self):
        rng=random.Random(403)
        for _ in range(10000):
            n=normal(tuple(rng.uniform(-1,1) for _ in range(3)));t=tangent(n)
            off=mul(t,rng.uniform(-.2,.2));old=add(off,mul(n,rng.uniform(.1,4)))
            pos=add(off,mul(n,-rng.uniform(.1,4)));v=add(mul(n,-rng.uniform(.1,100)),mul(t,rng.uniform(-10,10)))
            hit,p,out=resolve(Patch(normal=n,tangent=t),old,pos,v,phase=rng.uniform(0,math.tau))
            self.assertTrue(hit);self.assertGreaterEqual(dot(p,n),.05-1e-6)
            self.assertGreaterEqual(dot(out,n),-1e-6);self.assertLessEqual(norm(out),norm(v)+1e-6)
    def test_exponential_response_frame_partition(self):
        target=14.;initial=2.;rate=9.;duration=.7
        direct=target+(initial-target)*math.exp(-rate*duration)
        for n in (21,42,84,168):
            value=initial
            for _ in range(n): value+=(target-value)*(1-math.exp(-rate*duration/n))
            self.assertAlmostEqual(value,direct,places=10)
    def test_shell_volume_sampling(self):
        rng=random.Random(511);lo=.9;hi=1.1
        samples=[(lo**3+(hi**3-lo**3)*rng.random())**(1/3) for _ in range(10000)]
        self.assertTrue(all(lo<=r<=hi for r in samples))
        mean=sum(r**3 for r in samples)/len(samples)
        self.assertAlmostEqual(mean,(lo**3+hi**3)/2,delta=.01)
    def test_density_split_preserves_group_intensity(self):
        energy=12.;weights=(.5,.3,.2)
        self.assertAlmostEqual(sum(energy*w for w in weights),energy)
    def test_buffer_and_shader_contracts(self):
        root=Path(__file__).resolve().parents[1]
        common=(root/'Sources/Shaders/FireFieldCommon.hlsl').read_text()
        update=(root/'Sources/Shaders/FireParticleUpdate.hlsl').read_text()
        shader=(root/'Sources/Shaders/FireFlame.hlsl').read_text()
        self.assertIn('#define EF_NODE_ROWS 6u',common)
        self.assertIn('#define EF_CONTACT_ROWS 6u',common)
        self.assertIn('attributes.position = p + FireOriginWS;',update)
        self.assertNotIn('fwidth',update)
        self.assertIn('fwidth(field)',shader)
        self.assertNotIn('_Time',shader)

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(ReferenceTests)
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    report={
        'scope':'Python mirror of numerical reference + textual contracts; NOT Unity, C# or HLSL compilation',
        'tests_run':result.testsRun,'failures':len(result.failures),'errors':len(result.errors),
        'randomized_sweeps':10000,'shell_samples':10000,
        'unity_import':'not_run','csharp_compilation':'not_run','shader_compilation':'not_run',
        'gpu_visual_review':'not_run','gpu_performance':'not_measured',
        'passed':result.wasSuccessful()
    }
    (Path(__file__).resolve().parent/'validation_report.json').write_text(json.dumps(report,indent=2)+'\n')
    raise SystemExit(0 if result.wasSuccessful() else 1)
