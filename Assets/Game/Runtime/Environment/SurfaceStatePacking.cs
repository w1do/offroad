using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using UnityEngine;

// Lossless, in-memory history for unloaded route patches. No disk persistence.
public static class SurfaceStatePacking
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void WarmUp()
    {
        // Pay the compression/JIT startup cost while the scene is loading.
        var values=new float[1];var packed=Pack(values,values,values);Restore(packed,values,values,values);
        var pixels=new Color32[1];Restore(Pack(pixels),pixels);
    }
    static byte[] Compress(byte[] buffer,int length)
    {
        using(var output=new MemoryStream()){
            using(var zip=new DeflateStream(output,System.IO.Compression.CompressionLevel.Fastest,true))zip.Write(buffer,0,length);
            return output.ToArray();
        }
    }
    static bool Expand(byte[] packed,byte[] buffer,int length)
    {
        using(var input=new MemoryStream(packed,false))
        using(var zip=new DeflateStream(input,CompressionMode.Decompress)){
            int offset=0;
            while(offset<length){int read=zip.Read(buffer,offset,length-offset);if(read==0)return false;offset+=read;}
            return zip.ReadByte()==-1;
        }
    }
    public static byte[] Pack(float[] depths,float[] compaction,float[] berms)
    {
        int size=checked(depths.Length*sizeof(float)),length=checked(size*3);
        var buffer=ArrayPool<byte>.Shared.Rent(length);
        try{
            Buffer.BlockCopy(depths,0,buffer,0,size);Buffer.BlockCopy(compaction,0,buffer,size,size);Buffer.BlockCopy(berms,0,buffer,size*2,size);
            return Compress(buffer,length);
        }finally{ArrayPool<byte>.Shared.Return(buffer);}
    }
    public static bool Restore(byte[] packed,float[] depths,float[] compaction,float[] berms)
    {
        int size=checked(depths.Length*sizeof(float)),length=checked(size*3);
        var buffer=ArrayPool<byte>.Shared.Rent(length);
        try{
            if(!Expand(packed,buffer,length))return false;
            Buffer.BlockCopy(buffer,0,depths,0,size);Buffer.BlockCopy(buffer,size,compaction,0,size);Buffer.BlockCopy(buffer,size*2,berms,0,size);
            return true;
        }finally{ArrayPool<byte>.Shared.Return(buffer);}
    }
    public static byte[] Pack(Color32[] pixels)
    {
        int length=checked(pixels.Length*4);var buffer=ArrayPool<byte>.Shared.Rent(length);
        try{
            for(int i=0;i<pixels.Length;i++){var p=pixels[i];int j=i*4;buffer[j]=p.r;buffer[j+1]=p.g;buffer[j+2]=p.b;buffer[j+3]=p.a;}
            return Compress(buffer,length);
        }finally{ArrayPool<byte>.Shared.Return(buffer);}
    }
    public static bool Restore(byte[] packed,Color32[] pixels)
    {
        int length=checked(pixels.Length*4);var buffer=ArrayPool<byte>.Shared.Rent(length);
        try{
            if(!Expand(packed,buffer,length))return false;
            for(int i=0;i<pixels.Length;i++){int j=i*4;pixels[i]=new Color32(buffer[j],buffer[j+1],buffer[j+2],buffer[j+3]);}
            return true;
        }finally{ArrayPool<byte>.Shared.Return(buffer);}
    }
}
