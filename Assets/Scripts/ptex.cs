using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ptex : MonoBehaviour
{
    [SerializeField]
    private Shader shader4;
    [SerializeField]
    private Shader shader8;
    [SerializeField]
    private Shader shader16;
    [SerializeField]
    private Shader shader32;
    [SerializeField]
    private Shader shader64;

    [SerializeField]
    private Texture2D atlasTexture4;
    [SerializeField]
    private Texture2D atlasTexture8;
    [SerializeField]
    private Texture2D atlasTexture16;
    [SerializeField]
    private Texture2D atlasTexture32;
    [SerializeField]
    private Texture2D atlasTexture64;

    [DllImport("ptex-for-unity", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern IntPtr CreatePtexTexture(string fileName);

    [DllImport("ptex-for-unity", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern bool GetMeshType(IntPtr ptex, IntPtr buffer);

    [DllImport("ptex-for-unity", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern bool GetDataType(IntPtr ptex, IntPtr buffer);

    [DllImport("ptex-for-unity", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int GetNumChannels(IntPtr ptex);

    [DllImport("ptex-for-unity", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern bool GetBorderMode(IntPtr ptex, IntPtr buffer, bool isU);

    [DllImport("ptex-for-unity", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int GetNumFaces(IntPtr ptex);

    [DllImport("ptex-for-unity", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern bool GetHasMipMaps(IntPtr ptex);

    [DllImport("ptex-for-unity", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int GetFaceInfoResSize(IntPtr ptex, int faceId);

    [DllImport("ptex-for-unity", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern void GetTexture(IntPtr ptex, int faceId, IntPtr buffer);

    [DllImport("ptex-for-unity", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern void GetFixedTexture(IntPtr ptex, int faceId, IntPtr buffer, int uRes, int vRes);

    [DllImport("ptex-for-unity", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern void DestroyPtexTexture(IntPtr ptex);

    [DllImport("ptex-for-unity", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int Test();

    MeshFilter meshFilter;
    Mesh mesh;
    IntPtr _ptex;
    IntPtr bufferPtr;
    // 全テクスチャの解像度を同じにする
    // 2×2=4の解像度のテクスチャを256×256枚
    int uLogRes = 3;
    int vLogRes = 3;
    int texGroupCount = 16384;
    int sideTexCount = 128;
    int atlasWidth = 4096;
    int atlasHeight = 4096;

    void GetFixedResTextures()
    {
        int faceDataSize;
        // get ptex texture
        byte[] buffData;
        int offsetX = 0;
        int offsetY = 0;
        Texture2D[] srcTextures = new Texture2D[texGroupCount];
        // [[width, height],[width, height], ...]
        int[][] texInfo = new int[texGroupCount][];
        int uRes = (int)Math.Pow(2, uLogRes);
        int vRes = (int)Math.Pow(2, vLogRes);
        int channelNum = GetNumChannels(_ptex);
        faceDataSize = 1 * channelNum * uRes * vRes;
        Marshal.FreeHGlobal(bufferPtr);
        bufferPtr = Marshal.AllocHGlobal(faceDataSize);
        buffData = new byte[faceDataSize];

        atlasTexture16 = new Texture2D(uRes * sideTexCount, vRes * sideTexCount, TextureFormat.RGB24, false);
        int atlasNum = GetNumFaces(_ptex) / texGroupCount;
        for (int i = 0; i < atlasNum + 1; i++)
        {
            for (int groupIndex = 0; groupIndex < texGroupCount; groupIndex++)
            {
                print(GetFaceInfoResSize(_ptex, i * texGroupCount + groupIndex));
                if (texInfo[groupIndex] == null)
                {
                    texInfo[groupIndex] = new int[2];
                }
                texInfo[groupIndex][0] = uRes;
                texInfo[groupIndex][1] = vRes;
                GetFixedTexture(_ptex, i * texGroupCount + groupIndex, bufferPtr, uLogRes, vLogRes);
                Marshal.Copy(bufferPtr, buffData, 0, faceDataSize);
                if (srcTextures[groupIndex] == null)
                {
                    srcTextures[groupIndex] = new Texture2D(uRes, vRes, TextureFormat.RGB24, false);
                }
                srcTextures[groupIndex].LoadRawTextureData(buffData);
                srcTextures[groupIndex].Apply();
            }

            // draw to atlas texture
            for (int j = 0; j < texGroupCount; j++)
            {
                Color[] pixels = srcTextures[j].GetPixels();
                atlasTexture16.SetPixels(offsetX, offsetY, texInfo[j][0], texInfo[j][1], pixels);
                offsetX += uRes;
                if (uRes * sideTexCount <= offsetX)
                {
                    offsetX = 0;
                    offsetY += vRes;
                }
            }

            atlasTexture16.Apply();
            //material4.SetTexture("_MainTex", atlasTexture16);
            string path = "Assets/Textures/";
            string fileName = i.ToString() + "bunny" + ".png";
            System.IO.File.WriteAllBytes(path + fileName, atlasTexture16.EncodeToPNG());
            AssetDatabase.Refresh();
            offsetX = 0;
            offsetY = 0;
        }
    }

    struct Offset
    {
        public int u, v;
    }
    struct UV
    {
        public int u, v;
        public int res;
    }

    void GetQuadTextures()
    {
        int faceNum = GetNumFaces(_ptex);
        int faceDataSize;
        // get ptex texture
        byte[] buffData;
        Offset[] offsets = new Offset[5]; // side length 4, 8, 16, 32, 64
        UV[] atlasUV = new UV[faceNum];// faceidと解像度, uvインデックスの関係性

        int channelNum = GetNumChannels(_ptex);
        atlasTexture4 = new Texture2D(atlasWidth, atlasHeight, TextureFormat.R8, false);
        atlasTexture8 = new Texture2D(atlasWidth, atlasHeight, TextureFormat.R8, false);
        atlasTexture16 = new Texture2D(atlasWidth, atlasHeight, TextureFormat.R8, false);
        atlasTexture32 = new Texture2D(atlasWidth, atlasHeight, TextureFormat.R8, false);
        atlasTexture64 = new Texture2D(atlasWidth, atlasHeight, TextureFormat.R8, false);

        for (int i = 0; i < faceNum; i++)
        {
            int res = GetFaceInfoResSize(_ptex, i);
            int sideLength = (int)Math.Sqrt(res);
            faceDataSize = 1 * channelNum * res;
            Marshal.FreeHGlobal(bufferPtr);
            bufferPtr = Marshal.AllocHGlobal(faceDataSize);
            buffData = new byte[faceDataSize];
            GetTexture(_ptex, i, bufferPtr);
            Marshal.Copy(bufferPtr, buffData, 0, faceDataSize);

            Texture2D srcTexture;
            srcTexture = new Texture2D(sideLength, sideLength, TextureFormat.R8, false);
            srcTexture.LoadRawTextureData(buffData);
            srcTexture.Apply();

            // draw to atlas texture
            Color[] pixels = srcTexture.GetPixels();
            if (sideLength == 4)
            {
                atlasUV[i].u = offsets[0].u;
                atlasUV[i].v = offsets[0].v;
                atlasUV[i].res = 4;
                atlasTexture4.SetPixels(offsets[0].u, offsets[0].v, sideLength, sideLength, pixels);
                offsets[0].u += sideLength;
                if (atlasWidth <= offsets[0].u)
                {
                    offsets[0].u = 0;
                    offsets[0].v += sideLength;
                }
            }
            else if (sideLength == 8)
            {
                atlasUV[i].u = offsets[1].u;
                atlasUV[i].v = offsets[1].v;
                atlasUV[i].res = 8;
                atlasTexture8.SetPixels(offsets[1].u, offsets[1].v, sideLength, sideLength, pixels);
                offsets[1].u += sideLength;
                if (atlasWidth <= offsets[1].u)
                {
                    offsets[1].u = 0;
                    offsets[1].v += sideLength;
                }
            }
            else if (sideLength == 16)
            {
                atlasUV[i].u = offsets[2].u;
                atlasUV[i].v = offsets[2].v;
                atlasUV[i].res = 16;
                atlasTexture16.SetPixels(offsets[2].u, offsets[2].v, sideLength, sideLength, pixels);
                offsets[2].u += sideLength;
                if (atlasWidth <= offsets[2].u)
                {
                    offsets[2].u = 0;
                    offsets[2].v += sideLength;
                }
            }
            else if (sideLength == 32)
            {
                atlasUV[i].u = offsets[3].u;
                atlasUV[i].v = offsets[3].v;
                atlasUV[i].res = 32;
                atlasTexture32.SetPixels(offsets[3].u, offsets[3].v, sideLength, sideLength, pixels);
                offsets[3].u += sideLength;
                if (atlasWidth <= offsets[3].u)
                {
                    offsets[3].u = 0;
                    offsets[3].v += sideLength;
                }
            }
            else if (sideLength == 64)
            {
                atlasUV[i].u = offsets[4].u;
                atlasUV[i].v = offsets[4].v;
                atlasUV[i].res = 64;
                atlasTexture64.SetPixels(offsets[4].u, offsets[4].v, sideLength, sideLength, pixels);
                offsets[4].u += sideLength;
                if (atlasWidth <= offsets[4].u)
                {
                    offsets[4].u = 0;
                    offsets[4].v += sideLength;
                }
            }
            Destroy(srcTexture);
        }

        atlasTexture4.Apply();

        string path = "Assets/Textures/";
        string fileName = 4.ToString() + "teapot" + ".png";
        System.IO.File.WriteAllBytes(path + fileName, atlasTexture4.EncodeToPNG());

        atlasTexture8.Apply();
        fileName = 8.ToString() + "teapot" + ".png";
        System.IO.File.WriteAllBytes(path + fileName, atlasTexture8.EncodeToPNG());

        atlasTexture16.Apply();
        fileName = 16.ToString() + "teapot" + ".png";
        System.IO.File.WriteAllBytes(path + fileName, atlasTexture16.EncodeToPNG());

        atlasTexture32.Apply();
        fileName = 32.ToString() + "teapot" + ".png";
        System.IO.File.WriteAllBytes(path + fileName, atlasTexture32.EncodeToPNG());

        atlasTexture64.Apply();
        fileName = 64.ToString() + "teapot" + ".png";
        System.IO.File.WriteAllBytes(path + fileName, atlasTexture64.EncodeToPNG());
        AssetDatabase.Refresh();

        // update mesh structure and prepare submesh
        if (mesh.subMeshCount != 5)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.GetTriangles(0);
            int newIndex = 0;

            // devide into submesh
            List<int> submesh4 = new List<int>();
            List<int> submesh8 = new List<int>();
            List<int> submesh16 = new List<int>();
            List<int> submesh32 = new List<int>();
            List<int> submesh64 = new List<int>();
            List<Vector3> newVertices = new List<Vector3>();
            List<Vector2> atlasUVs = new List<Vector2>();
            List<Vector2> localUVs = new List<Vector2>();

            for (int i = 0; i < faceNum; i++)
            {
                int index0 = triangles[i * 3 + 0];
                int index1 = triangles[i * 3 + 1];
                int index2 = triangles[i * 3 + 2];
                int index3 = triangles[i * 3 + 3];
                newVertices.Add(vertices[index0]);
                newVertices.Add(vertices[index1]);
                newVertices.Add(vertices[index2]);
                newVertices.Add(vertices[index3]);
                atlasUVs.Add(new Vector2(atlasUV[i].u / (float)atlasWidth, atlasUV[i].v / (float)atlasHeight));
                atlasUVs.Add(new Vector2(atlasUV[i].u / (float)atlasWidth, atlasUV[i].v / (float)atlasHeight));
                atlasUVs.Add(new Vector2(atlasUV[i].u / (float)atlasWidth, atlasUV[i].v / (float)atlasHeight));
                atlasUVs.Add(new Vector2(atlasUV[i].u / (float)atlasWidth, atlasUV[i].v / (float)atlasHeight));
                localUVs.Add(new Vector2(0, 0));
                localUVs.Add(new Vector2(1, 0));
                localUVs.Add(new Vector2(0, 1));
                localUVs.Add(new Vector2(1, 1));
                if (atlasUV[i].res == 4)
                {
                    submesh4.Add(newIndex);
                    submesh4.Add(newIndex + 1);
                    submesh4.Add(newIndex + 2);
                    submesh4.Add(newIndex + 3);

                }
                else if (atlasUV[i].res == 8)
                {
                    submesh8.Add(newIndex);
                    submesh8.Add(newIndex + 1);
                    submesh8.Add(newIndex + 2);
                    submesh8.Add(newIndex + 3);
                }
                else if (atlasUV[i].res == 16)
                {
                    submesh16.Add(newIndex);
                    submesh16.Add(newIndex + 1);
                    submesh16.Add(newIndex + 2);
                    submesh16.Add(newIndex + 3);
                }
                else if (atlasUV[i].res == 32)
                {
                    submesh32.Add(newIndex);
                    submesh32.Add(newIndex + 1);
                    submesh32.Add(newIndex + 2);
                    submesh32.Add(newIndex + 3);
                }
                else if (atlasUV[i].res == 64)
                {
                    submesh64.Add(newIndex);
                    submesh64.Add(newIndex + 1);
                    submesh64.Add(newIndex + 2);
                    submesh64.Add(newIndex + 3);
                }
                newIndex += 4;
            }
            mesh.Clear();
            mesh.SetVertices(newVertices);
            mesh.SetUVs(0, atlasUVs);
            mesh.SetUVs(1, localUVs);

            mesh.subMeshCount = 1;
            mesh.SetIndices(submesh4, MeshTopology.Quads, 0);
            mesh.subMeshCount = 2;
            mesh.SetIndices(submesh8, MeshTopology.Quads, 1);
            mesh.subMeshCount = 3;
            mesh.SetIndices(submesh16, MeshTopology.Quads, 2);
            mesh.subMeshCount = 4;
            mesh.SetIndices(submesh32, MeshTopology.Quads, 3);
            mesh.subMeshCount = 5;
            mesh.SetIndices(submesh64, MeshTopology.Quads, 4);
        }

        Material material4 = new Material(shader4);
        material4.SetTexture("_tex", atlasTexture4);
        Material material8 = new Material(shader8);
        material8.SetTexture("_tex", atlasTexture8);
        Material material16 = new Material(shader16);
        material16.SetTexture("_tex", atlasTexture16);
        Material material32 = new Material(shader32);
        material32.SetTexture("_tex", atlasTexture32);
        Material material64 = new Material(shader64);
        material64.SetTexture("_tex", atlasTexture64);
        Renderer renderer = GetComponent<Renderer>();
        renderer.sharedMaterials = new Material[] { material4, material8, material16, material32, material64 };
        print("vertices length: " + mesh.vertices.Length);
    }

    void GetTriangleTextures()
    {
        int triangleNum = GetNumFaces(_ptex);
        int faceDataSize;
        // get ptex texture
        byte[] buffData;
        Offset[] offsets = new Offset[5]; // side length 4, 8, 16, 32, 64
        UV[] atlasUV = new UV[triangleNum];// faceidと解像度, uvインデックスの関係性

        int channelNum = GetNumChannels(_ptex);
        atlasTexture4 = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGB24, false);
        atlasTexture8 = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGB24, false);
        atlasTexture16 = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGB24, false);
        atlasTexture32 = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGB24, false);
        atlasTexture64 = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGB24, false);

        for (int i = 0; i < triangleNum; i++)
        {
            int res = GetFaceInfoResSize(_ptex, i);
            int sideLength = (int)Math.Sqrt(res);
            faceDataSize = 1 * channelNum * res;
            Marshal.FreeHGlobal(bufferPtr);
            bufferPtr = Marshal.AllocHGlobal(faceDataSize);
            buffData = new byte[faceDataSize];
            GetTexture(_ptex, i, bufferPtr);
            Marshal.Copy(bufferPtr, buffData, 0, faceDataSize);

            Texture2D srcTexture;
            srcTexture = new Texture2D(sideLength, sideLength, TextureFormat.RGB24, false);
            srcTexture.LoadRawTextureData(buffData);
            srcTexture.Apply();

            // draw to atlas texture
            Color[] pixels = srcTexture.GetPixels();
            if (sideLength == 4)
            {
                atlasUV[i].u = offsets[0].u;
                atlasUV[i].v = offsets[0].v;
                atlasUV[i].res = 4;
                atlasTexture4.SetPixels(offsets[0].u, offsets[0].v, sideLength, sideLength, pixels);
                offsets[0].u += sideLength;
                if (atlasWidth <= offsets[0].u)
                {
                    offsets[0].u = 0;
                    offsets[0].v += sideLength;
                }
            }
            else if (sideLength == 8)
            {
                atlasUV[i].u = offsets[1].u;
                atlasUV[i].v = offsets[1].v;
                atlasUV[i].res = 8;
                atlasTexture8.SetPixels(offsets[1].u, offsets[1].v, sideLength, sideLength, pixels);
                offsets[1].u += sideLength;
                if (atlasWidth <= offsets[1].u)
                {
                    offsets[1].u = 0;
                    offsets[1].v += sideLength;
                }
            }
            else if (sideLength == 16)
            {
                atlasUV[i].u = offsets[2].u;
                atlasUV[i].v = offsets[2].v;
                atlasUV[i].res = 16;
                atlasTexture16.SetPixels(offsets[2].u, offsets[2].v, sideLength, sideLength, pixels);
                offsets[2].u += sideLength;
                if (atlasWidth <= offsets[2].u)
                {
                    offsets[2].u = 0;
                    offsets[2].v += sideLength;
                }
            }
            else if (sideLength == 32)
            {
                atlasUV[i].u = offsets[3].u;
                atlasUV[i].v = offsets[3].v;
                atlasUV[i].res = 32;
                atlasTexture32.SetPixels(offsets[3].u, offsets[3].v, sideLength, sideLength, pixels);
                offsets[3].u += sideLength;
                if (atlasWidth <= offsets[3].u)
                {
                    offsets[3].u = 0;
                    offsets[3].v += sideLength;
                }
            }
            else if (sideLength == 64)
            {
                atlasUV[i].u = offsets[4].u;
                atlasUV[i].v = offsets[4].v;
                atlasUV[i].res = 64;
                atlasTexture64.SetPixels(offsets[4].u, offsets[4].v, sideLength, sideLength, pixels);
                offsets[4].u += sideLength;
                if (atlasWidth <= offsets[4].u)
                {
                    offsets[4].u = 0;
                    offsets[4].v += sideLength;
                }
            }
            Destroy(srcTexture);
        }


        atlasTexture4.wrapModeU = TextureWrapMode.Mirror;
        atlasTexture4.wrapModeV = TextureWrapMode.Mirror;
        atlasTexture4.filterMode = FilterMode.Point;

        atlasTexture8.wrapModeU = TextureWrapMode.Mirror;
        atlasTexture8.wrapModeV = TextureWrapMode.Mirror;
        atlasTexture8.filterMode = FilterMode.Point;

        atlasTexture16.wrapModeU = TextureWrapMode.Mirror;
        atlasTexture16.wrapModeV = TextureWrapMode.Mirror;
        atlasTexture16.filterMode = FilterMode.Point;

        atlasTexture32.wrapModeU = TextureWrapMode.Mirror;
        atlasTexture32.wrapModeV = TextureWrapMode.Mirror;
        atlasTexture32.filterMode = FilterMode.Point;

        atlasTexture64.filterMode = FilterMode.Point;

        string path = "Assets/Textures/";
        atlasTexture4.Apply();
        string fileName = 4.ToString() + "bunny" + ".png";
        System.IO.File.WriteAllBytes(path + fileName, atlasTexture4.EncodeToPNG());

        atlasTexture8.Apply();
        fileName = 8.ToString() + "bunny" + ".png";
        System.IO.File.WriteAllBytes(path + fileName, atlasTexture8.EncodeToPNG());

        atlasTexture16.Apply();
        fileName = 16.ToString() + "bunny" + ".png";
        System.IO.File.WriteAllBytes(path + fileName, atlasTexture16.EncodeToPNG());

        atlasTexture32.Apply();
        fileName = 32.ToString() + "bunny" + ".png";
        System.IO.File.WriteAllBytes(path + fileName, atlasTexture32.EncodeToPNG());

        atlasTexture64.Apply();
        fileName = 64.ToString() + "bunny" + ".png";
        System.IO.File.WriteAllBytes(path + fileName, atlasTexture64.EncodeToPNG());
        AssetDatabase.Refresh();



        // update mesh structure and prepare submesh
        if (mesh.subMeshCount != 5)
        {

            Vector3[] vertices = mesh.vertices;
            int[] triangleIndices = mesh.GetTriangles(0);
            int newIndex = 0;

            // devide into submesh
            List<int> submesh4 = new List<int>();
            List<int> submesh8 = new List<int>();
            List<int> submesh16 = new List<int>();
            List<int> submesh32 = new List<int>();
            List<int> submesh64 = new List<int>();
            List<Vector3> newVertices = new List<Vector3>();
            List<Vector2> atlasUVs = new List<Vector2>();
            List<Vector2> localUVs = new List<Vector2>();

            for (int i = 0; i < triangleNum; i++)
            {
                int index0 = triangleIndices[i * 3 + 0];
                int index1 = triangleIndices[i * 3 + 1];
                int index2 = triangleIndices[i * 3 + 2];

                newVertices.Add(vertices[index0]);
                newVertices.Add(vertices[index1]);
                newVertices.Add(vertices[index2]);
                atlasUVs.Add(new Vector2(atlasUV[i].u / (float)atlasWidth, atlasUV[i].v / (float)atlasHeight));
                atlasUVs.Add(new Vector2(atlasUV[i].u / (float)atlasWidth, atlasUV[i].v / (float)atlasHeight));
                atlasUVs.Add(new Vector2(atlasUV[i].u / (float)atlasWidth, atlasUV[i].v / (float)atlasHeight));
                localUVs.Add(new Vector2(0, 0));
                localUVs.Add(new Vector2(0, 1));
                localUVs.Add(new Vector2(1, 0));
                if (atlasUV[i].res == 4)
                {
                    submesh4.Add(newIndex);
                    submesh4.Add(newIndex + 1);
                    submesh4.Add(newIndex + 2);

                }
                else if (atlasUV[i].res == 8)
                {
                    submesh8.Add(newIndex);
                    submesh8.Add(newIndex + 1);
                    submesh8.Add(newIndex + 2);
                }
                else if (atlasUV[i].res == 16)
                {
                    submesh16.Add(newIndex);
                    submesh16.Add(newIndex + 1);
                    submesh16.Add(newIndex + 2);
                }
                else if (atlasUV[i].res == 32)
                {
                    submesh32.Add(newIndex);
                    submesh32.Add(newIndex + 1);
                    submesh32.Add(newIndex + 2);
                }
                else if (atlasUV[i].res == 64)
                {
                    submesh64.Add(newIndex);
                    submesh64.Add(newIndex + 1);
                    submesh64.Add(newIndex + 2);
                }
                newIndex += 3;
            }
            mesh.Clear();
            mesh.SetVertices(newVertices);
            mesh.SetUVs(0, atlasUVs);
            mesh.SetUVs(1, localUVs);

            mesh.subMeshCount = 1;
            mesh.SetTriangles(submesh4, 0);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(submesh8, 1);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(submesh16, 2);
            mesh.subMeshCount = 4;
            mesh.SetTriangles(submesh32, 3);
            mesh.subMeshCount = 5;
            mesh.SetTriangles(submesh64, 4);
        }



        Material material4 = new Material(shader4);
        material4.SetTexture("_tex", atlasTexture4);
        Material material8 = new Material(shader8);
        material8.SetTexture("_tex", atlasTexture8);
        Material material16 = new Material(shader16);
        material16.SetTexture("_tex", atlasTexture16);
        Material material32 = new Material(shader32);
        material32.SetTexture("_tex", atlasTexture32);
        Material material64 = new Material(shader64);
        material64.SetTexture("_tex", atlasTexture64);
        Renderer renderer = GetComponent<Renderer>();
        renderer.sharedMaterials = new Material[] { material4, material8, material16, material32, material64 };
        print("vertices length: " + mesh.vertices.Length);
    }

    // Start is called before the first frame update
    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        mesh = meshFilter.sharedMesh;

        // load ptex
        print("loading" + Application.dataPath + "/Textures/bunny.ptx");
        _ptex = CreatePtexTexture(Application.dataPath + "/Textures/bunny.ptx");

        // log for ptex info
        bufferPtr = Marshal.AllocHGlobal(256);
        GetMeshType(_ptex, bufferPtr);
        print("meshType:" + Marshal.PtrToStringAnsi(bufferPtr));

        GetDataType(_ptex, bufferPtr);
        print("dataType:" + Marshal.PtrToStringAnsi(bufferPtr));

        print("numChannels:" + GetNumChannels(_ptex));

        GetBorderMode(_ptex, bufferPtr, true);
        print("uBorderMode:" + Marshal.PtrToStringAnsi(bufferPtr));

        GetBorderMode(_ptex, bufferPtr, false);
        print("vBorderMode:" + Marshal.PtrToStringAnsi(bufferPtr));

        print("numFaces:" + GetNumFaces(_ptex));

        //print("hasMipMaps:" + GetHasMipMaps(_ptex));
        GetTriangleTextures();
        //GetFixedResTextures();
    }

    // Update is called once per frame
    void Update()
    {

    }
    private void OnDestroy()
    {
        Marshal.FreeHGlobal(bufferPtr);
        DestroyPtexTexture(_ptex);
    }
}
