import urllib.request, re, json, pathlib, concurrent.futures, zipfile, io, http.cookiejar
opener=urllib.request.build_opener(urllib.request.ProxyHandler({}),urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def get(url):
 return opener.open(urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0'}),timeout=45).read()
s=get('https://opengameart.org/content/car-kit').decode()
print('\n'.join(re.findall(r'https?[^"<> ]+\.zip',s)))
for name in ['Asphalt012','Ground037','Rock030','Ice004']:
 try:
  j=json.loads(get('https://ambientcg.com/api/v2/full_json?include=downloadData&q='+name+'&limit=1'))
  print(name,[(a['assetId'],a.get('downloadFolders')) for a in j['foundAssets']])
 except Exception as e: print(name,str(e))
pathlib.Path('.firecrawl/opengameart.html').write_text(s,encoding='utf8')
