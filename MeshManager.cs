using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;

public class MeshManager
{
    public Mesh CreateMeshFromDescriptor(Mesh parent, SubMeshDescriptor subMeshDescriptor)
    {
        Mesh mesh = new Mesh();

        int vertexoffsetV = subMeshDescriptor.baseVertex;
        int indexoffsetV = subMeshDescriptor.indexStart;

        Vector2[] uv = new Vector2[subMeshDescriptor.vertexCount];
        Vector3[] vertices = new Vector3[subMeshDescriptor.vertexCount];
        Vector3[] normals = new Vector3[subMeshDescriptor.vertexCount];
        Vector4[] tangents = new Vector4[subMeshDescriptor.vertexCount];
        BoneWeight[] boneWeights = new BoneWeight[subMeshDescriptor.vertexCount];

        int[] triangles = new int[subMeshDescriptor.indexCount];

        int ioffsetV = indexoffsetV;

        HashSet<int> indexes = new HashSet<int>();
        Dictionary<int, int> oldIdxToNew = new Dictionary<int, int>();

        //beggin from triangles to rebuild vertices later
        for (int i = 0; i < subMeshDescriptor.indexCount; i += 3)
        {
            triangles[i] = parent.triangles[ioffsetV + i];

            triangles[i + 1] = parent.triangles[ioffsetV + i + 1];

            triangles[i + 2] = parent.triangles[ioffsetV + i + 2];

            //hashset do not store repeated numbers
            indexes.Add(triangles[i]);
            indexes.Add(triangles[i + 1]);
            indexes.Add(triangles[i + 2]);
        }

        int idx = 0;
        List<BlendShapeVertexData> blendShapesData = new List<BlendShapeVertexData>();
        List<BlendShapeVertexData> newBlendShapesDeltas = new List<BlendShapeVertexData>();

        #region getting blendshapes data

        BlendShapeVertexData deltas = new BlendShapeVertexData(parent.vertexCount);

        int count = parent.blendShapeCount;

        for (int b = 0; b < count; b++)
        {
            parent.GetBlendShapeFrameVertices(b, 1, deltas.deltaVertices, deltas.deltaNormals, deltas.deltaTangents);

            string n = parent.GetBlendShapeName(b);

            blendShapesData.Add(deltas);
        }

        #endregion

        foreach (int i in indexes)
        {
            oldIdxToNew.Add(i, idx);

            vertices[idx] = parent.vertices[i];
            uv[idx] = parent.uv[i];
            normals[idx] = parent.normals[i];
            tangents[idx] = parent.tangents[i];

            #region creating new blendshapes data

            for (int k = 0; k < blendShapesData.Count; k++)
            {
                newBlendShapesDeltas.Add(new BlendShapeVertexData(subMeshDescriptor.vertexCount));

                newBlendShapesDeltas[k].deltaNormals[idx] = deltas.deltaNormals[i];
                newBlendShapesDeltas[k].deltaVertices[idx] = deltas.deltaVertices[i];
                newBlendShapesDeltas[k].deltaTangents[idx] = deltas.deltaTangents[i];

            }
            #endregion

            #region bones weights
            boneWeights[idx] = new BoneWeight();

            boneWeights[idx].boneIndex0 = parent.boneWeights[i].boneIndex0;
            boneWeights[idx].boneIndex1 = parent.boneWeights[i].boneIndex1;
            boneWeights[idx].boneIndex2 = parent.boneWeights[i].boneIndex2;
            boneWeights[idx].boneIndex3 = parent.boneWeights[i].boneIndex3;

            boneWeights[idx].weight0 = parent.boneWeights[i].weight0;
            boneWeights[idx].weight1 = parent.boneWeights[i].weight1;
            boneWeights[idx].weight2 = parent.boneWeights[i].weight2;
            boneWeights[idx].weight3 = parent.boneWeights[i].weight3;
            #endregion

            idx++;
        }

        #region setting up blendshapes data
        for (int i = 0; i < blendShapesData.Count; i++)
        {

            mesh.AddBlendShapeFrame(
                parent.GetBlendShapeName(i),
                1,
                newBlendShapesDeltas[i].deltaVertices,
                newBlendShapesDeltas[i].deltaNormals,
                newBlendShapesDeltas[i].deltaTangents
                );

        }


        #endregion           

        #region realign triangles indexes
        int t = 3;

        for (int i = 0; i < subMeshDescriptor.indexCount; i += t)
        {
            for (int k = 0; k < t; k++)
            {
                triangles[i + k] = oldIdxToNew[triangles[i + k]];
            }
        }

        #endregion

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.uv = uv;
        mesh.triangles = triangles;

        mesh.bindposes = parent.bindposes;
        mesh.boneWeights = boneWeights;

        return mesh;
    }
    public Mesh[] GetMeshSubmeshes(Mesh mesh)
    {
        Mesh[] submeshes = new Mesh[mesh.subMeshCount];

        for (int j = 0; j < mesh.subMeshCount; j++)
        {
            SubMeshDescriptor subMeshDescriptor = mesh.GetSubMesh(j);

            submeshes[j] = CreateMeshFromDescriptor(mesh, subMeshDescriptor);
        }

        return submeshes;
    }
    public Mesh CombineMeshes(Mesh[] meshes)
    {
        Mesh m = new Mesh();

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector4> tangents = new List<Vector4>();
        List<Vector2> uv0 = new List<Vector2>();
        List<BoneWeight> boneWeights = new List<BoneWeight>();

        int offsetV = 0;

        for (int i = 0; i < meshes.Length; i++)
        {
            Mesh mesh = meshes[i];

            triangles.AddRange(mesh.triangles);
            vertices.AddRange(mesh.vertices);
            normals.AddRange(mesh.normals);
            uv0.AddRange(mesh.uv);
            tangents.AddRange(mesh.tangents);
            boneWeights.AddRange(mesh.boneWeights);

            for (int j = 0; j < mesh.triangles.Length; j++)
            {
                triangles[j] = mesh.triangles[j] + offsetV;
            }

            offsetV += mesh.vertexCount;
        }

        m.vertices = vertices.ToArray();

        m.boneWeights = boneWeights.ToArray();
        m.triangles = triangles.ToArray();

        return m;
    }
    public void CombineSkinnedMeshesAll(SkinnedMeshRenderer skinnedMeshRenderer, SkinnedMeshRenderer[] parts, Rect[] uvs = null)
    {
        if (parts.Length == 0)
            return;

        List<Transform> bones = new List<Transform>();
        List<Matrix4x4> bindposes = new List<Matrix4x4>();

        Dictionary<string, int[]> blendshapes = new Dictionary<string, int[]>();
        List<List<int>> submeshTriangles = new List<List<int>>();
        List<Material> materials = new List<Material>();
        List<Dictionary<int, int>> bonesRemapList = new List<Dictionary<int, int>>();
        Transform rootBone = parts[0].rootBone;

        List<Vector3> vertex = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector4> tangents = new List<Vector4>();
        List<Vector2> uv0 = new List<Vector2>();
        List<BoneWeight> boneWeights = new List<BoneWeight>();

        int totalVertices = 0;
        int totalTriangles = 0;
        int totalSubmeshes = 0;

        #region organizing materials, bones and triangles order
        //skinned mesh relative data
        for (int i = 0; i < parts.Length; i++)
        {
            SkinnedMeshRenderer skinnedMeshesData = parts[i];
            Dictionary<int, int> currentSkinnedMeshBonesRemap = new Dictionary<int, int>();
            Dictionary<int, int> currentSubmeshMaterialRemap = new Dictionary<int, int>();

            //////////////  
            vertex.AddRange(parts[i].sharedMesh.vertices);
            normals.AddRange(parts[i].sharedMesh.normals);
            boneWeights.AddRange(parts[i].sharedMesh.boneWeights);
            tangents.AddRange(parts[i].sharedMesh.tangents);
            uv0.AddRange(parts[i].sharedMesh.uv);
            /////////////

            #region materials list
            for (int i1 = 0; i1 < parts[i].sharedMaterials.Length; i1++)
            {

                Material mat = parts[i].sharedMaterials[i1];

                if (!materials.Find(n => n.name == mat.name))
                {
                    currentSubmeshMaterialRemap.Add(i1, materials.Count);
                    materials.Add(mat);

                }
                else
                {
                    currentSubmeshMaterialRemap.Add(i1, i1);
                }
            }
            #endregion

            #region add and remap bones in a very slow way
            int bonesLength = skinnedMeshesData.bones.Length;

            for (int b = 0; b < bonesLength; b++)
            {
                Transform bone = skinnedMeshesData.bones[b];

                if (!bones.Contains(bone))
                {
                    currentSkinnedMeshBonesRemap.Add(b, bones.Count);

                    bones.Add(bone);

                    Matrix4x4 bindpose = parts[i].sharedMesh.bindposes[bones.Count - 1];
                    bindposes.Add(bindpose);

                }
                else
                {
                    currentSkinnedMeshBonesRemap.Add(b, b);
                }
            }

            #endregion

            for (int _k = 0; _k < parts[i].sharedMesh.subMeshCount; _k++)
            {

                int k = currentSubmeshMaterialRemap[_k];

                var sm = parts[i].sharedMesh.GetSubMesh(_k);
                int[] indices = parts[i].sharedMesh.GetIndices(_k);

                if (k >= submeshTriangles.Count) submeshTriangles.Add(new List<int>());

                for (int i1 = 0; i1 < indices.Length; i1++)
                {
                    //remapping indices to match new mesh vertices
                    indices[i1] += totalVertices;
                }

                submeshTriangles[k].AddRange(indices);

                totalSubmeshes++;
            }

            for (int v = 0; v < parts[i].sharedMesh.vertexCount; v++)
            {
                BoneWeight boneWeight = boneWeights[v + totalVertices];
                boneWeight.boneIndex0 = currentSkinnedMeshBonesRemap[boneWeight.boneIndex0];
                boneWeight.boneIndex1 = currentSkinnedMeshBonesRemap[boneWeight.boneIndex1];
                boneWeight.boneIndex2 = currentSkinnedMeshBonesRemap[boneWeight.boneIndex2];
                boneWeight.boneIndex3 = currentSkinnedMeshBonesRemap[boneWeight.boneIndex3];
                boneWeights[v + totalVertices] = boneWeight;

            }

            for (int k = 0; k < parts[i].sharedMesh.blendShapeCount; k++)
            {
                string key = parts[i].sharedMesh.GetBlendShapeName(k);

                if (!blendshapes.ContainsKey(key))
                    blendshapes[key] = new int[parts.Length];

                blendshapes[key][i] = k + 1;
            }

            totalVertices += parts[i].sharedMesh.vertexCount;
            totalTriangles += parts[i].sharedMesh.triangles.Length;
        }

        #endregion

        Mesh combined_new_mesh = new Mesh();

        #region triangles
        List<int> organizedTriagles = new List<int>();

        SubMeshDescriptor[] submehses = new SubMeshDescriptor[submeshTriangles.Count];
        int count = 0;

        for (int i = 0; i < submeshTriangles.Count; i++)
        {
            SubMeshDescriptor smd = new SubMeshDescriptor();

            smd.indexStart = count;

            organizedTriagles.AddRange(submeshTriangles[i]);

            smd.topology = MeshTopology.Triangles;
            smd.indexCount = submeshTriangles[i].Count;
            count += submeshTriangles[i].Count;

            submehses[i] = smd;

        }
        #region setting mesh
        ////////////////
        combined_new_mesh.vertices = vertex.ToArray();
        combined_new_mesh.triangles = organizedTriagles.ToArray();
        combined_new_mesh.boneWeights = boneWeights.ToArray();
        combined_new_mesh.normals = normals.ToArray();
        ////////////////
        #endregion
        for (int i1 = 0; i1 < submehses.Length; i1++)
        {
            combined_new_mesh.subMeshCount++;
            combined_new_mesh.SetSubMesh(i1, submehses[i1]);
        }

        #endregion

        #region  shape keys


        int offsetV = 0;

        Vector3[] deltaVertices = null;
        Vector3[] deltaTangents = null;
        Vector3[] deltaNormals = null;

        if (blendshapes.Count > 0)
        {
            deltaVertices = new Vector3[combined_new_mesh.vertexCount];
            deltaTangents = new Vector3[combined_new_mesh.vertexCount];
            deltaNormals = new Vector3[combined_new_mesh.vertexCount];
        }


        //We assume all blendshapes only have a single frame, aka 0 (empty) to 1 (full). 
        //So we just copy the last frame in each blendshape to a weight of 1 
        foreach (KeyValuePair<string, int[]> shape in blendshapes)
        {
            offsetV = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                int vcount = parts[i].sharedMesh.vertexCount;

                //No blendshape for this mesh
                if (shape.Value[i] == 0)
                {
                    //TODO: Research whether it's better to create a new array initially, or manually clear them as needed
                    System.Array.Clear(deltaVertices, offsetV, vcount);
                    System.Array.Clear(deltaTangents, offsetV, vcount);
                    System.Array.Clear(deltaNormals, offsetV, vcount);

                    offsetV += vcount;
                    continue;
                }

                //Since GetBlendShapeFrameVertices requires matching sizes of arrays, we gotta create these every time -_-
                Vector3[] tempDeltaVertices = new Vector3[vcount];
                Vector3[] tempDeltaTangents = new Vector3[vcount];
                Vector3[] tempDeltaNormals = new Vector3[vcount];

                int frame = (parts[i].sharedMesh.GetBlendShapeFrameCount(shape.Value[i] - 1) - 1);

                parts[i].sharedMesh.GetBlendShapeFrameVertices(shape.Value[i] - 1, frame, tempDeltaVertices, tempDeltaNormals, tempDeltaTangents);

                System.Array.Copy(tempDeltaVertices, 0, deltaVertices, offsetV, vcount);
                System.Array.Copy(tempDeltaNormals, 0, deltaNormals, offsetV, vcount);
                System.Array.Copy(tempDeltaTangents, 0, deltaTangents, offsetV, vcount);

                offsetV += vcount;
            }

            //Apply
            combined_new_mesh.AddBlendShapeFrame(shape.Key, 1, deltaVertices, deltaNormals, deltaTangents);
        }

        #endregion

        combined_new_mesh.bindposes = bindposes.ToArray();

        combined_new_mesh.Optimize();
        combined_new_mesh.RecalculateBounds();
        combined_new_mesh.RecalculateTangents();

        skinnedMeshRenderer.sharedMesh = combined_new_mesh;
        skinnedMeshRenderer.sharedMaterials = materials.ToArray();
        skinnedMeshRenderer.bones = bones.ToArray();
        skinnedMeshRenderer.rootBone = rootBone;
    }
    public void CombineSkinnedMeshes(SkinnedMeshRenderer skinnedMeshRenderer, SkinnedMeshRenderer[] parts, Rect[] uvs = null)
    {
        if (parts.Length == 0)
            return;

        Dictionary<string, int[]> blendshapes = new Dictionary<string, int[]>();
        List<List<int>> submeshTriangles = new List<List<int>>();
        List<Material> materials = new List<Material>();

        List<Vector3> vertex = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector4> tangents = new List<Vector4>();
        List<Vector2> uv0 = new List<Vector2>();
        List<BoneWeight> boneWeights = new List<BoneWeight>();

        int totalVertices = 0;
        int totalTriangles = 0;
        int totalSubmeshes = 0;

        #region organizing materials, bones and triangles order
        //skinned mesh relative data
        for (int i = 0; i < parts.Length; i++)
        {
            SkinnedMeshRenderer skinnedMeshesData = parts[i];
            Dictionary<int, int> currentSubmeshMaterialRemap = new Dictionary<int, int>();

            //////////////  
            vertex.AddRange(parts[i].sharedMesh.vertices);
            normals.AddRange(parts[i].sharedMesh.normals);
            boneWeights.AddRange(parts[i].sharedMesh.boneWeights);
            tangents.AddRange(parts[i].sharedMesh.tangents);
            uv0.AddRange(parts[i].sharedMesh.uv);
            /////////////

            #region materials list
            for (int i1 = 0; i1 < parts[i].sharedMaterials.Length; i1++)
            {

                Material mat = parts[i].sharedMaterials[i1];

                if (!materials.Find(n => n.name == mat.name))
                {
                    currentSubmeshMaterialRemap.Add(i1, materials.Count);
                    materials.Add(mat);

                }
                else
                {
                    currentSubmeshMaterialRemap.Add(i1, i1);
                }
            }
            #endregion

            for (int _k = 0; _k < parts[i].sharedMesh.subMeshCount; _k++)
            {

                int k = currentSubmeshMaterialRemap[_k];

                var sm = parts[i].sharedMesh.GetSubMesh(_k);
                int[] indices = parts[i].sharedMesh.GetIndices(_k);

                if (k >= submeshTriangles.Count) submeshTriangles.Add(new List<int>());

                for (int i1 = 0; i1 < indices.Length; i1++)
                {
                    //remapping indices to match new mesh vertices
                    indices[i1] += totalVertices;
                }

                submeshTriangles[k].AddRange(indices);

                totalSubmeshes++;
            }

            for (int k = 0; k < parts[i].sharedMesh.blendShapeCount; k++)
            {
                string key = parts[i].sharedMesh.GetBlendShapeName(k);

                if (!blendshapes.ContainsKey(key))
                    blendshapes[key] = new int[parts.Length];

                blendshapes[key][i] = k + 1;
            }

            totalVertices += parts[i].sharedMesh.vertexCount;
            totalTriangles += parts[i].sharedMesh.triangles.Length;
        }

        #endregion

        Mesh combined_new_mesh = new Mesh();

        #region triangles
        List<int> organizedTriagles = new List<int>();

        SubMeshDescriptor[] submehses = new SubMeshDescriptor[submeshTriangles.Count];
        int count = 0;

        for (int i = 0; i < submeshTriangles.Count; i++)
        {
            SubMeshDescriptor smd = new SubMeshDescriptor();

            smd.indexStart = count;

            organizedTriagles.AddRange(submeshTriangles[i]);

            smd.topology = MeshTopology.Triangles;
            smd.indexCount = submeshTriangles[i].Count;
            count += submeshTriangles[i].Count;

            submehses[i] = smd;

        }
        #region setting mesh
        ////////////////
        combined_new_mesh.vertices = vertex.ToArray();
        combined_new_mesh.triangles = organizedTriagles.ToArray();
        combined_new_mesh.boneWeights = boneWeights.ToArray();
        combined_new_mesh.normals = normals.ToArray();
        ////////////////
        #endregion
        for (int i1 = 0; i1 < submehses.Length; i1++)
        {
            combined_new_mesh.subMeshCount++;
            combined_new_mesh.SetSubMesh(i1, submehses[i1]);
        }

        #endregion

        #region  shape keys


        int offsetV = 0;

        Vector3[] deltaVertices = null;
        Vector3[] deltaTangents = null;
        Vector3[] deltaNormals = null;

        if (blendshapes.Count > 0)
        {
            deltaVertices = new Vector3[combined_new_mesh.vertexCount];
            deltaTangents = new Vector3[combined_new_mesh.vertexCount];
            deltaNormals = new Vector3[combined_new_mesh.vertexCount];
        }


        //We assume all blendshapes only have a single frame, aka 0 (empty) to 1 (full). 
        //So we just copy the last frame in each blendshape to a weight of 1 
        foreach (KeyValuePair<string, int[]> shape in blendshapes)
        {
            offsetV = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                int vcount = parts[i].sharedMesh.vertexCount;

                //No blendshape for this mesh
                if (shape.Value[i] == 0)
                {
                    //TODO: Research whether it's better to create a new array initially, or manually clear them as needed
                    System.Array.Clear(deltaVertices, offsetV, vcount);
                    System.Array.Clear(deltaTangents, offsetV, vcount);
                    System.Array.Clear(deltaNormals, offsetV, vcount);

                    offsetV += vcount;
                    continue;
                }

                //Since GetBlendShapeFrameVertices requires matching sizes of arrays, we gotta create these every time -_-
                Vector3[] tempDeltaVertices = new Vector3[vcount];
                Vector3[] tempDeltaTangents = new Vector3[vcount];
                Vector3[] tempDeltaNormals = new Vector3[vcount];

                int frame = (parts[i].sharedMesh.GetBlendShapeFrameCount(shape.Value[i] - 1) - 1);

                parts[i].sharedMesh.GetBlendShapeFrameVertices(shape.Value[i] - 1, frame, tempDeltaVertices, tempDeltaNormals, tempDeltaTangents);

                System.Array.Copy(tempDeltaVertices, 0, deltaVertices, offsetV, vcount);
                System.Array.Copy(tempDeltaNormals, 0, deltaNormals, offsetV, vcount);
                System.Array.Copy(tempDeltaTangents, 0, deltaTangents, offsetV, vcount);

                offsetV += vcount;
            }

            //Apply
            combined_new_mesh.AddBlendShapeFrame(shape.Key, 1, deltaVertices, deltaNormals, deltaTangents);
        }

        #endregion

        combined_new_mesh.bindposes = parts[0].sharedMesh.bindposes;

        combined_new_mesh.Optimize();
        combined_new_mesh.RecalculateBounds();
        combined_new_mesh.RecalculateTangents();

        skinnedMeshRenderer.sharedMesh = combined_new_mesh;
        skinnedMeshRenderer.sharedMaterials = materials.ToArray();
        skinnedMeshRenderer.bones = parts[0].bones.ToArray();
        skinnedMeshRenderer.rootBone = parts[0].rootBone;
    }
    public Mesh CombineMeshesWithSubmeshes(Dictionary<Material, List<Mesh>> meshesByMaterial)
    {
        Mesh m = new Mesh();

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector4> tangents = new List<Vector4>();
        List<Vector2> uv0 = new List<Vector2>();
        List<BoneWeight> boneWeights = new List<BoneWeight>();

        int offsetV = 0;
        int offsetT = 0;

        SubMeshDescriptor[] subMeshes = new SubMeshDescriptor[meshesByMaterial.Keys.Count];

        int idx = 0;
        foreach(var meshes in meshesByMaterial)
        {   
            SubMeshDescriptor sm = new SubMeshDescriptor();

            int smStart = offsetT;
            int smVStart = offsetV;

            for (int i = 0; i < meshes.Value.Count; i++)
            {
                Mesh mesh = meshes.Value[i];

                triangles.AddRange(mesh.triangles);
                vertices.AddRange(mesh.vertices);
                normals.AddRange(mesh.normals);
                uv0.AddRange(mesh.uv);
                tangents.AddRange(mesh.tangents);
                boneWeights.AddRange(mesh.boneWeights);

                for (int j = 0; j < mesh.triangles.Length; j++)
                {
                    triangles[j + offsetT] = mesh.triangles[j] + offsetV;
                }

                offsetV += mesh.vertexCount;
                offsetT += mesh.triangles.Length;
            }

            sm.topology = MeshTopology.Triangles;
            sm.baseVertex = smVStart;
            sm.vertexCount = offsetV - smVStart;
            sm.indexStart = smStart;
            sm.indexCount =  offsetT - smStart;

            subMeshes[idx] = sm;
            idx++;
        
        }

        m.vertices = vertices.ToArray();
        m.boneWeights = boneWeights.ToArray();
        m.triangles = triangles.ToArray();
        m.uv = uv0.ToArray();
        m.tangents = tangents.ToArray();
        
        m.subMeshCount = subMeshes.Length;
        for (int i = 0; i < subMeshes.Length; i++)
        {
            SubMeshDescriptor s = subMeshes[i];
            m.SetSubMesh(i,subMeshes[i]);
        }

        return m;
    }

    public struct BlendShapeVertexData
    {
        public Vector3[] deltaVertices;
        public Vector3[] deltaTangents;
        public Vector3[] deltaNormals;

        public BlendShapeVertexData(int length)
        {
            this.deltaVertices = new Vector3[length];
            this.deltaTangents = new Vector3[length];
            this.deltaNormals = new Vector3[length];
        }
    }

}

public struct SkinnedMeshesData
{
    public SkinnedMeshRenderer skinnedMesh;
    public string parentBone;

}

