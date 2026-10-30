import io,json,pathlib,struct,zipfile

root=pathlib.Path('Assets/Game/Art/PajeroSport');root.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile('Models/Cars/2005-mitsubishi-pajero-sport.zip') as archive:
    data=archive.read('source/mitsubishi_pajero_sport_mk1_1996.glb')
size=struct.unpack_from('<I',data,12)[0];gltf=json.loads(data[20:20+size]);offset=20+size
binary_size=struct.unpack_from('<I',data,offset)[0];blob=data[offset+8:offset+8+binary_size]
formats={5126:'f',5125:'I',5123:'H',5121:'B'};widths={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}
def accessor(index):
    a=gltf['accessors'][index];view=gltf['bufferViews'][a['bufferView']];fmt='<'+formats[a['componentType']]*widths[a['type']];stride=view.get('byteStride',struct.calcsize(fmt));start=view.get('byteOffset',0)+a.get('byteOffset',0)
    return [struct.unpack_from(fmt,blob,start+i*stride) for i in range(a['count'])]
manifest={'materials':[],'parts':[]}
for m in gltf['materials']:
    pbr=m.get('pbrMetallicRoughness',{});manifest['materials'].append({'name':m['name'],'color':pbr.get('baseColorFactor',[1,1,1,1]),'metallic':pbr.get('metallicFactor',1),'roughness':pbr.get('roughnessFactor',1),'transparent':m.get('alphaMode')=='BLEND'})
for i,m in enumerate(gltf['meshes']):
    for j,p in enumerate(m['primitives']):
        positions=accessor(p['attributes']['POSITION']);normals=accessor(p['attributes']['NORMAL']);uvs=accessor(p['attributes']['TEXCOORD_0']) if 'TEXCOORD_0' in p['attributes'] else [(0,0)]*len(positions);indices=[x[0] for x in accessor(p['indices'])]
        path=root/f'part_{i:02}_{j}.bytes'
        with path.open('wb') as out:
            out.write(struct.pack('<ii',len(positions),len(indices)))
            for v,n,uv in zip(positions,normals,uvs):
                # The archived mesh itself is centimetres, roof toward -Y and nose toward -Z.
                # Reflect all axes and reverse winding to keep Unity normals/culling consistent.
                out.write(struct.pack('<8f',*[-x*.01 for x in v],*[-x for x in n],uv[0],1-uv[1]))
            for t in range(0,len(indices),3):out.write(struct.pack('<iii',indices[t],indices[t+2],indices[t+1]))
        manifest['parts'].append({'name':m['name'],'file':path.name,'material':p['material'],'wheel':p['material'] in (15,16)})
(root/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(root/'SOURCE.txt').write_text('User-supplied Models/Cars/2005-mitsubishi-pajero-sport.zip\nInternal GLB name: mitsubishi_pajero_sport_mk1_1996.glb. Archive filename is not proof of model-year accuracy.\nOriginal mesh geometry and material colors preserved. Converted to metres / Unity coordinates; dedicated tire and rim meshes only are animated. No third-party model was substituted.\n',encoding='utf-8')
road=pathlib.Path('Assets/Game/Art/RoadMud');road.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile('Models/Road/forest-mud-puddle.zip') as z:
    with zipfile.ZipFile(io.BytesIO(z.read('source/forest-puddle.zip'))) as nested:
        for entry in nested.infolist():
            if entry.is_dir():continue
            target=road/pathlib.PurePosixPath(entry.filename).name
            target.write_bytes(nested.read(entry))
(road/'SOURCE.txt').write_text('User-supplied Models/Road/forest-mud-puddle.zip, nested source/forest-puddle.zip. Original FBX and photographic color/normal maps.\n',encoding='utf-8')
print(f'Converted {len(manifest["parts"])} Pajero mesh parts / {len(manifest["materials"])} original materials; extracted road mud FBX and maps.')

