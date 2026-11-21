#!/bin/bash
# Runs a race on every circuit and prints one line per track.
# usage: ./summary.sh <laps> <grid> <aiPace 0-100>      (env: AGGR=0-100, SETUP=low|high, TT=1, TRACE=DEM T0= T1=)
cd "$(dirname "$0")"
dotnet build -nologo -v q -c Release >/dev/null || exit 1
for t in 0 1 2 3 4 5; do
  dotnet bin/Release/net8.0/RaceSim.dll $t $1 $2 $3 2>&1 | python3 -c "
import sys,re
lines=sys.stdin.read().splitlines()
hdr=[l for l in lines if l.startswith('==')][0]; mx=[l for l in lines if l.startswith('max')][0]
est=float(re.search(r'est lap ([0-9.]+)',hdr).group(1)); jumps=int(re.search(r'jumps=([0-9]+)',mx).group(1))
rows=[l for l in lines if re.match(r'^ ?[0-9]+\. ',l)]
best=sorted(float(re.search(r'best=([0-9.]+)',r).group(1)) for r in rows if re.search(r'best=[0-9]',r))
off=sum(int(re.search(r'off=([0-9]+)',r).group(1)) for r in rows)
dnf=sum(1 for r in rows if 'DNF' in r)
pits=[int(re.search(r'pits=([0-9]+)',r).group(1)) for r in rows]
print(f'{hdr[3:24]:22s} tahmin={est:5.1f}s  enIyi={best[0]:6.2f}s  medyan={best[len(best)//2]:6.2f}s  sifirlama={jumps:3d}  pistDisi={off/100:6.1f}s  dnf={dnf}  pit/arac={sum(pits)/len(pits):.1f}')
"
done
