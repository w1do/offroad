import urllib.request,zipfile,io,pathlib,concurrent.futures
root=pathlib.Path('Assets/MountainDrive/Art');root.mkdir(parents=True,exist_ok=True)
def download(item):
 name,url=item
 o=urllib.request.build_opener(urllib.request.ProxyHandler({}))
 try:
  b=o.open(urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0'}),timeout=60).read()
  z=zipfile.ZipFile(io.BytesIO(b)); dest=root/name;dest.mkdir(exist_ok=True)
  z.extractall(dest)
  return name,len(b),z.namelist()[:12]
 except Exception as e:return name,str(e)
items=[('Kenney','https://opengameart.org/sites/default/files/kenney_car-kit_3.1.zip')]
for n in ['Asphalt012','Ground037','Rock030','Ice004']:
 items.append((n,'https://ambientcg.com/get?file='+n+'_1K-JPG.zip'))
with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:
 for r in pool.map(download,items):print(r)
