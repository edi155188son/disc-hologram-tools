"""python/bdgeom.py and BdAmp --geo-check must agree exactly on the BD spiral geometry.
usage: check_geometry.py [path/to/BdAmp]"""
import os, subprocess, sys, tempfile, pathlib
here = pathlib.Path(__file__).resolve().parent.parent
bdamp = sys.argv[1] if len(sys.argv) > 1 else str(here / 'artifacts/bin/BdAmp/Release/net10.0/BdAmp')
with tempfile.TemporaryDirectory() as tmp:
    subprocess.run([sys.executable, str(here / 'tests/make_test_bdaf.py'), tmp], check=True, stdout=subprocess.DEVNULL)
    py = subprocess.run([sys.executable, 'bdgeom.py'], cwd=here / 'python', check=True, capture_output=True, text=True).stdout
    cs = subprocess.run([bdamp, '--inner', '23.987042157339335', '--outer', '57.987960341783236', '--geo-check',
                         '0,1234.5,100000.25,5000000.75,12000000', '--fields', f'{tmp}/blk.bdaf', '--output', f'{tmp}/x.iso'],
                        check=True, capture_output=True, text=True).stdout
pyrows = [l.split()[1:] for l in py.splitlines() if l.startswith('PY ')]
csrows = [l.split()[1:] for l in cs.splitlines() if l.startswith('GEO ')]
assert len(pyrows) == len(csrows) == 5, (pyrows, csrows)
bad = [(a, b) for a, b in zip(pyrows, csrows) if [float(x) for x in a] != [float(x) for x in b]]
if bad:
    print('geometry MISMATCH', bad); sys.exit(1)
print('geometry: python/bdgeom.py == BdAmp --geo-check (bit-identical, 5 sample sectors)')
