import pathlib,shutil,urllib.request,zipfile,io
root=pathlib.Path('Assets/MountainDrive/Art').resolve(); dest=pathlib.Path('SourceAssets').resolve();dest.mkdir(exist_ok=True)
for p in list(root.rglob('*')):
 if p.is_file() and p.suffix in ['.blend','.usdc','.mtlx','.tres']:
  t=dest/p.relative_to(root);t.parent.mkdir(parents=True,exist_ok=True);shutil.move(str(p),str(t))
o=urllib.request.build_opener(urllib.request.ProxyHandler({}))
b=o.open('https://ambientcg.com/get?file=Asphalt004_1K-JPG.zip',timeout=40).read();z=zipfile.ZipFile(io.BytesIO(b));d=root/'Asphalt004';d.mkdir(exist_ok=True)
for n in z.namelist():
 if n.endswith('.jpg'): (d/n).write_bytes(z.read(n))
print('Clean asphalt downloaded. Non-Unity source files stored in SourceAssets.')
