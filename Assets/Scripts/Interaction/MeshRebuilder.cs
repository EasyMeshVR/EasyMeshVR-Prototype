using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using EasyMeshVR.Core;
using EasyMeshVR.Multiplayer;

public class MeshRebuilder : MonoBehaviour, IOnEventCallback
{
    public static MeshRebuilder instance { get; private set; }

    [SerializeField]
    public GameObject editingSpace;
    public GameObject model;
    
    // Holds the vertex and edge prefabs
    public GameObject vertex;
    public GameObject edge;

    public GameObject face;

    // Mesh data
    Mesh mesh;
    public Vector3[] vertices;
    Vector3 vertexPosition;
    public int[] triangles;

    // Stores the vertex/edge visual data, i.e. which edges are connected to which vertices
    // Mostly accessed in MoveVertices.cs (and eventually MoveEdges.cs)
    //public static Dictionary<GameObject, List<int>> visuals;
    public List<Edge> edgeObjects;
    public List<Vertex> vertexObjects;

    public List<Face> faceObjects;


    // Setup
    public void Start()
    {
        edgeObjects = new List<Edge>();
        vertexObjects = new List<Vertex>();
        faceObjects = new List<Face>();

        instance = this;

        editingSpace = GameObject.FindGameObjectWithTag(Constants.EDITING_SPACE_TAG);

        // For importing in real time we would need the script to get the model automatically
        model = gameObject;
        model.tag = ("Model");

        // Copy vertices and triangles
        mesh = GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        triangles = mesh.triangles;

        // Start visualizing the mesh
        RemoveDuplicates();
        CreateVisuals();
    }

    void OnEnable()
    {

        PhotonNetwork.AddCallbackTarget(this);
    }

    void OnDisable()
    {

        PhotonNetwork.RemoveCallbackTarget(this);
    }

    // Deletes the duplicate vertices Unity and STL files create
    // Re-references those duplicate vertices in the triangles array with unique ones only
    void RemoveDuplicates()
    {
        // Filter out unique vertices and triangles, and store indices of every duplicate of a vertex (2 or more dupes)
        HashSet<Vector3> vertexUnique = new HashSet<Vector3>();
        Dictionary<List<int>, Vector3> vertexDuplicate = new Dictionary<List<int>, Vector3>();
        List<int> triangleUnique = new List<int>();

        // Loop over the vertices array, separating duplicates and uniques
        for (int i = 0; i < vertices.Length; i++)
        {
            // List for each index to add 
            List<int> dupeVert = new List<int>();

            // If the hashset already has the vertex, it's a duplicate
            if (vertexUnique.Contains(vertices[i]))
            {
                // If this is not the first duplicate of the vertex, get the previous list, remove the entry, add new index, readd entry
                if (vertexDuplicate.ContainsValue(vertices[i]))
                {
                    List<int> indicies = vertexDuplicate.FirstOrDefault(x => x.Value == vertices[i]).Key;
                    vertexDuplicate.Remove(indicies);
                    indicies.Add(i);
                    vertexDuplicate.Add(indicies, vertices[i]);
                }
                dupeVert.Add(i);
                vertexDuplicate.Add(dupeVert, vertices[i]);
            }
            else
            {
                vertexUnique.Add(vertices[i]);
            }
        }

        // Loop over the triangles array
        for (int i = 0; i < triangles.Length; i++)
        {
            // Check if vertex in triangles array is a duplicate, replace with original if it is
            if (vertexDuplicate.ContainsValue(vertices[triangles[i]]))
            {
                // Need to loop through vertexUnique for imported meshes, for meshes in Unity the first set of vertices are unique
                int j = 0;
                foreach (Vector3 vertex in vertexUnique)
                {
                    if (vertex == vertices[triangles[i]])
                    {
                        triangleUnique.Add(j);
                        break;
                    }
                    j++;
                }
            }
            else
            {
                triangleUnique.Add(triangles[i]);
            }
        }

        // Copy unique vertices to array
        Vector3[] newVertices = new Vector3[vertexUnique.Count];
        int v = 0;
        foreach (Vector3 vertex in vertexUnique)
            newVertices[v++] = vertex;
        vertices = newVertices;

        // Copy unique triangles to array
        int[] newTriangles = new int[triangleUnique.Count];
        int t = 0;
        foreach (int triangle in triangleUnique)
            newTriangles[t++] = triangle;
        triangles = newTriangles;

        // Update the mesh filter with new unique vertex and triangle data
        mesh.Clear();
        mesh.vertices = newVertices;
        mesh.triangles = newTriangles;
        mesh.RecalculateNormals();
    }

    // Actually create the vertex and edge GameObject interactables
    void CreateVisuals()
    {
        // Repeats for every vertex stored in the mesh filter
        for (int i = 0; i < vertices.Length; i++)
        {
            // Create a new vertex from a prefab, make it a child of the mesh and set it's position
            GameObject newVertex = Instantiate(vertex, model.transform);
            newVertex.transform.localPosition = vertices[i];
            newVertex.name = "Vertex" + i.ToString();

            // Set the id of the Vertex component to be the index in the vertices array
            Vertex vertexObj = newVertex.GetComponent<Vertex>();
            vertexObj.id = i;
            vertexObjects.Add(vertexObj);

            // Save vertices adjacent to the one we're currently looking at (no duplicates)
            HashSet<int> adjacentVertices = new HashSet<int>();

            // Loop through the triangles array and look for the adjacent vertices
            for (int j = 0; j < triangles.Length; j += 3)
            {
                // Triangles are created in triplets
                // Entering "0, 1, 2," in the triangles array would make a triangle

                if (triangles[j] == i) // First index of triplet
                {
                    adjacentVertices.Add(triangles[j + 1]);
                    adjacentVertices.Add(triangles[j + 2]);
                }
                else if (triangles[j + 1] == i) // Second index of triplet
                {
                    adjacentVertices.Add(triangles[j]);
                    adjacentVertices.Add(triangles[j + 2]);
                }
                else if (triangles[j + 2] == i) // Third index of triplet
                {
                    adjacentVertices.Add(triangles[j]);
                    adjacentVertices.Add(triangles[j + 1]);
                }
            }

            // Connect a line from our starting vertex to each adjacent vertex
            foreach (int k in adjacentVertices)
            {
                // Ignore adjacent vertices we've already dealt with
                if (k < i)
                    continue;

                // Same as vertex, create a new edge object and set its parent
                GameObject newEdge = Instantiate(edge, model.transform);

                // Set the edge's position to between the two vertices and scale it appropriately
                float edgeDistance = 0.5f * Vector3.Distance(vertices[i], vertices[k]);
                newEdge.transform.localPosition = (vertices[i] + vertices[k]) / 2;
                newEdge.transform.localScale = new Vector3(newEdge.transform.localScale.x, edgeDistance, newEdge.transform.localScale.z);

                // Orient the edge to look at the vertices
                newEdge.transform.LookAt(newVertex.transform, Vector3.up);
                newEdge.transform.rotation *= Quaternion.Euler(90, 0, 0);

                // Add edge and it's connecting vertices to a dictionary reference for use in other scripts
                Edge edgeComponent = newEdge.GetComponent<Edge>();
                edgeComponent.id = edgeObjects.Count();
                edgeComponent.vert1 = i;
                edgeComponent.vert2 = k;
                edgeObjects.Add(edgeComponent);
            }
        }

        // Triangle handles
        for(int i = 0; i < triangles.Length; i+=3)
        {
            GameObject newFace = Instantiate(face, model.transform);
            // Add face to list and get vertices
            Face faceComponent = newFace.GetComponent<Face>();
            faceComponent.id = faceObjects.Count();
            faceComponent.vert1 = triangles[i];
            faceComponent.vert2 = triangles[i+1];
            faceComponent.vert3 = triangles[i+2];

            faceObjects.Add(faceComponent);
            // Place face object in center of triangle
            float totalX = vertices[faceComponent.vert1].x + vertices[faceComponent.vert2].x + vertices[faceComponent.vert3].x;
            float totalY = vertices[faceComponent.vert1].y + vertices[faceComponent.vert2].y + vertices[faceComponent.vert3].y;
            float totalZ = vertices[faceComponent.vert1].z + vertices[faceComponent.vert2].z + vertices[faceComponent.vert3].z;

            foreach(Edge edge in edgeObjects)
            {
                if((edge.vert1 == faceComponent.vert1 && edge.vert2 == faceComponent.vert2) || (edge.vert2 == faceComponent.vert1 && edge.vert1 == faceComponent.vert2))
                    faceComponent.edge1 = edge.id;
                if((edge.vert1 == faceComponent.vert2 && edge.vert2 == faceComponent.vert3) || (edge.vert2 == faceComponent.vert2 && edge.vert1 == faceComponent.vert3))
                    faceComponent.edge2 = edge.id;
                if((edge.vert1 == faceComponent.vert1 && edge.vert2 == faceComponent.vert3) || (edge.vert2 == faceComponent.vert1 && edge.vert1 == faceComponent.vert3))
                    faceComponent.edge3 = edge.id;
            }

            newFace.transform.localPosition = new Vector3(totalX/3, totalY/3, totalZ/3);
        }
    }

    public void ClearHeldDataForPlayer(Player player)
    {
        Debug.LogFormat("Called ClearHeldDataForPlayer for player Name: {0} ActorNumber: {1}", player.NickName, player.ActorNumber);

        // We start a coroutine since this may be a long running process
        // and we don't want to hang the main thread
        StartCoroutine(ClearHeldData(player));
    }

    private IEnumerator ClearHeldData(Player player)
    {
        foreach (Vertex vertexObj in vertexObjects)
        {
            if (vertexObj.isHeldByOther && vertexObj.heldByActorNumber == player.ActorNumber)
            {
                Debug.LogFormat("Cleared vertex id: {0} held by player Name: {1} ActorNumber: {2}", vertexObj.id, player.NickName, player.ActorNumber);
                vertexObj.isHeldByOther = false;
                vertexObj.heldByActorNumber = -1;
            }

            yield return null;
        }

        foreach (Edge edgeObj in edgeObjects)
        {
            if (edgeObj.isHeldByOther && edgeObj.heldByActorNumber == player.ActorNumber)
            {
                Debug.LogFormat("Cleared edge id: {0} held by player Name: {1} ActorNumber: {2}", edgeObj.id, player.NickName, player.ActorNumber);
                edgeObj.isHeldByOther = false;
                edgeObj.heldByActorNumber = -1;
                edgeObj.locked = false;
                edgeObj.GetComponent<MoveEdge>().SetActiveEdges(edgeObj, true);
            }

            yield return null;
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        byte eventCode = photonEvent.Code;

        if (photonEvent.CustomData == null)
        {
            return;
        }

        switch (eventCode)
        {
            case Constants.MESH_VERTEX_PULL_EVENT_CODE:
            {
                object[] data = (object[])photonEvent.CustomData;
                HandleMeshVertexPullEvent(data);
                break;
            }
            case Constants.MESH_EDGE_PULL_EVENT_CODE:
            {
                object[] data = (object[])photonEvent.CustomData;
                HandleMeshEdgePullEvent(data);
                break;
            }
            default:
                break;
        }
    }

    private void HandleMeshVertexPullEvent(object[] data)
    {
        VertexPullEvent vertexEvent = VertexPullEvent.DeserializeEvent(data);

        Vector3 vertexPos = vertexEvent.vertexPos;
        int index = vertexEvent.id;
        bool released = vertexEvent.released;
        Vertex vertexObj = vertexObjects[index];
        MoveVertices moveVertices = vertexObj.GetComponent<MoveVertices>();
        vertexObj.transform.localPosition = vertexPos;
        vertexObj.isHeldByOther = !released;
        vertexObj.heldByActorNumber = (released) ? -1 : vertexEvent.actorNumber;
        vertices[index] = vertexPos;
        moveVertices.UpdateMesh(index);
    }

    private void HandleMeshEdgePullEvent(object[] data)
    {
        EdgePullEvent edgeEvent = EdgePullEvent.DeserializeEvent(data);

        Edge edgeObj = edgeObjects[edgeEvent.id];
        Vertex vert1Obj = vertexObjects[edgeEvent.vert1];
        Vertex vert2Obj = vertexObjects[edgeEvent.vert2];
        MoveEdge moveEdge = edgeObj.GetComponent<MoveEdge>();
        int heldByActorNumber = (edgeEvent.released) ? -1 : edgeEvent.actorNumber;
        edgeObj.isHeldByOther = vert1Obj.isHeldByOther = vert2Obj.isHeldByOther = !edgeEvent.released;
        edgeObj.heldByActorNumber = vert1Obj.heldByActorNumber = vert2Obj.heldByActorNumber = heldByActorNumber;
        vert1Obj.transform.localPosition = edgeEvent.vertex1Pos;
        vert2Obj.transform.localPosition = edgeEvent.vertex2Pos;
        vertices[edgeEvent.vert1] = edgeEvent.vertex1Pos;
        vertices[edgeEvent.vert2] = edgeEvent.vertex2Pos;
        moveEdge.SetActiveEdges(edgeObj, edgeEvent.released);
        moveEdge.UpdateMesh(edgeEvent.id, edgeEvent.vert1, edgeEvent.vert2, false);
    }
}
