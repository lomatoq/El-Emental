"""Summarize actual benchmark JSON; differences are whole-frame observations, not isolated GPU costs."""
import argparse,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('report',type=Path);a=p.parse_args()
r=json.loads(a.report.read_text(encoding='utf-8-sig'));print(r['status'],r['reason']);print(r['scope'])
for view in range(2):
 blocks=[b for b in r['results'] if b['view']==view]
 print('\nVIEW',view)
 for b in blocks:
  print('block',b['block'],'mode',b['mode'],'samples',b['samples'],'GPU valid',b['gpuWholeFrame']['valid'],'GPU p95',b['gpuWholeFrame']['p95'],'publisher CPU p95',b['publisherCpu']['p95'],'pass CPU p95',b['passCpu']['p95'],'GC max',b['gc']['max'],'pose error',b['maxCameraPositionError'],b['maxCameraAngleError'])
 for metric in ('cpuFrame','gpuWholeFrame','passCpu'):
  bymode={m:[b[metric]['p50'] for b in blocks if b['mode']==m and b[metric]['valid']>=540] for m in range(3)}
  if all(len(v)==2 for v in bymode.values()):
   means={m:sum(v)/len(v) for m,v in bymode.items()};print(metric,'paired-order mean block-median deltas fog-off',means[1]-means[0],'clouds-fog',means[2]-means[1],'OBSERVED, not isolated attribution')
  else: print(metric,'UNAVAILABLE or insufficient valid sample coverage (need >=90% each block)')
if r['rows']!=7200 or r['width']!=1920 or r['height']!=1080:raise SystemExit('INVALID measurement coverage/resolution')
if any(b['maxCameraPositionError']>0.01 or b['maxCameraAngleError']>0.1 for b in r['results']):raise SystemExit('INVALID camera invariance; investigate before interpreting deltas')
