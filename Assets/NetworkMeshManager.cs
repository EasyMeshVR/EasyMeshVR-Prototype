using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

namespace EasyMeshVR.Multiplayer
{
    [RequireComponent(typeof(PhotonView))]
    public class NetworkMeshManager : MonoBehaviour
    {
        #region Public Fields

        public static NetworkMeshManager instance;

        #endregion

        #region Private Fields

        [SerializeField]
        private GameObject meshObjectPrefab;

        [SerializeField]
        private Transform meshObjectInitialTransform;

        private PhotonView photonView;

        #endregion

        #region MonoBehaviour Callbacks

        void Awake()
        {
            instance = this;
        }

        void Start()
        {
            photonView = GetComponent<PhotonView>();
        }

        #endregion

        #region RPCs

        [PunRPC]
        void InstantiateMeshes(Vector3[][] verts, Vector3[][] norms, int[][] tris)
        {
            if (verts == null || norms ==  null || tris == null)
            {
                Debug.LogError("InstantiateMeshes RPC failed because input is null");
                return;
            }
            /*if (meshes == null || meshes.Length < 1)
            {
                Debug.LogError("Meshes array is null or empty");
                return;
            }*/

            GameObject parent = new GameObject("Model");
            parent.transform.position = meshObjectInitialTransform.position;
            parent.transform.rotation = meshObjectInitialTransform.rotation;

            for (int i = 0; i < verts.Length; ++i)
            {
                GameObject meshObject = Instantiate(meshObjectPrefab);
                meshObject.transform.SetParent(parent.transform, false);

                meshObject.name += "(" + i + ")";

                Mesh mesh = new Mesh();
                mesh.name = "Mesh-" + name + "(" + i + ")";
                mesh.vertices = verts[i];
                mesh.normals = norms[i];
                mesh.triangles = tris[i];
                mesh.RecalculateNormals();
                meshObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            Debug.LogFormat("Instantiating {0} meshes", verts.Length);

            /*for (int i = 0; i < meshes.Length; ++i)
            {
                GameObject meshObject = Instantiate(meshObjectPrefab);
                meshObject.transform.SetParent(parent.transform, false);

                meshObject.name += "(" + i + ")";

                Mesh mesh = meshes[i];
                mesh.name = "Mesh-" + name + "(" + i + ")";
                meshObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            Debug.LogFormat("Instantiating {0} meshes", meshes.Length);*/
        }

        #endregion

        #region Public Methods

        public void SynchronizeMeshImport(Mesh[] meshes)
        {
            // TODO: this does not work with large meshes (tested with a 6MB stl file size)
            // Find a way to send the data more compactly (binary compression?)
            // or spaced out in multiple RPCs (maybe one for each mesh?)

            Vector3[][] verts = new Vector3[meshes.Length][];
            Vector3[][] norms = new Vector3[meshes.Length][];
            int[][] tris = new int[meshes.Length][];

            for (int i = 0; i < meshes.Length; ++i)
            {
                verts[i] = meshes[i].vertices;
                norms[i] = meshes[i].normals;
                tris[i] = meshes[i].triangles;
            }

            photonView.RPC("InstantiateMeshes", RpcTarget.AllBuffered, verts, norms, tris);
        }

        #endregion
    }
}
