using System;
using UnityEngine;
using System.Collections.Generic;

namespace DefaultNamespace
{
    public class EndlessTerrain : MonoBehaviour
    {
        private const float viewerMoveThresholdForChunkUpdate = 25f;
        private const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;
        
        public LODInfo[] detailLevels;
        public static float maxViewDistance;
        public Transform viewer;
        public Material mapMaterial;

        public static Vector2 viewerPosition;
        private Vector2 viewerPositionOld;
        
        private int chunkSize;
        private int visibleChunks;

        private static MapGenerator _mapGenerator;

        Dictionary<Vector2, TerrainChunk> terrainChunkDict = new Dictionary<Vector2, TerrainChunk>();

        private List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

        private void Start()
        {
            _mapGenerator = FindObjectOfType<MapGenerator>();
            maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshold;
            chunkSize = MapGenerator.mapChunkSize - 1;
            visibleChunks = Mathf.RoundToInt(maxViewDistance / chunkSize);
            
            UpdateVisibleChunks();
        }

        private void Update()
        {
            viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
            if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate)
            {
                viewerPositionOld = viewerPosition;
                UpdateVisibleChunks();
            }
        }

        void UpdateVisibleChunks()
        {
            foreach (TerrainChunk chunk in terrainChunksVisibleLastUpdate)
            {
                chunk.SetVisible(false);
            }

            terrainChunksVisibleLastUpdate.Clear();

            int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
            int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

            for (int yOffset = -visibleChunks; yOffset < visibleChunks; yOffset++)
            {
                for (int xOffset = -visibleChunks; xOffset < visibleChunks; xOffset++)
                {
                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                    if (terrainChunkDict.ContainsKey(viewedChunkCoord))
                    {
                        terrainChunkDict[viewedChunkCoord].UpdateTerrainChunk();
                        if (terrainChunkDict[viewedChunkCoord].IsVisible())
                        {
                            terrainChunksVisibleLastUpdate.Add(terrainChunkDict[viewedChunkCoord]);
                        }
                    }
                    else
                    {
                        terrainChunkDict.Add(viewedChunkCoord,
                            new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, transform, mapMaterial));
                    }
                }
            }
        }

        public class TerrainChunk
        {
            private GameObject _meshObject;
            private Vector2 _position;
            private Bounds _bounds;

            private MapData _mapData;

            private MeshRenderer _meshRenderer;
            private MeshFilter _meshFilter;

            private LODInfo[] detailLevels;
            private LODMesh[] _lodMeshes;

            private bool mapDataReceived;
            private int previousLODIndex = -1;

            public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material)
            {
                this.detailLevels = detailLevels;

                _position = coord * size;
                _bounds = new Bounds(_position, Vector2.one * size);
                Vector3 posV3 = new Vector3(_position.x, 0, _position.y);

                _meshObject = new GameObject("Terrain Chunk");
                _meshRenderer = _meshObject.AddComponent<MeshRenderer>();
                _meshFilter = _meshObject.AddComponent<MeshFilter>();
                _meshRenderer.material = material;

                _meshObject.transform.position = posV3;
                _meshObject.transform.parent = parent;
                SetVisible(false);

                _lodMeshes = new LODMesh[detailLevels.Length];

                for (int i = 0; i < detailLevels.Length; i++)
                {
                    _lodMeshes[i] = new LODMesh(detailLevels[i].lod);
                }

                _mapGenerator.RequestMapData(OnMapDataReceived);
            }

            public void UpdateTerrainChunk()
            {
                float viewerDistanceFromNearestEdge = Mathf.Sqrt(_bounds.SqrDistance(viewerPosition));
                bool visible = viewerDistanceFromNearestEdge <= maxViewDistance;
                if (mapDataReceived)
                { 
                    if (visible)
                    {
                        int lodIndex = 0;
                        for (int i = 0; i < detailLevels.Length - 1; i++)
                        {
                            if (viewerDistanceFromNearestEdge > detailLevels[i].visibleDistanceThreshold)
                            {
                                lodIndex = i + 1;
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (lodIndex != previousLODIndex)
                        {
                            LODMesh lodMesh = _lodMeshes[lodIndex];
                            if (lodMesh.hasMesh)
                            {
                                previousLODIndex = lodIndex;
                                _meshFilter.mesh = lodMesh.mesh;
                            }
                            else if (!lodMesh.hasRequestedMesh)
                            {
                                lodMesh.RequestMesh(_mapData);
                            }
                        }
                    }
                }

                SetVisible(visible);
            }

            public void SetVisible(bool visible)
            {
                _meshObject.SetActive(visible);
            }

            public bool IsVisible()
            {
                return _meshObject.activeSelf;
            }

            void OnMapDataReceived(MapData mapData)
            {
                this._mapData = mapData;
                this.mapDataReceived = true;
            }
        }

        class LODMesh
        {
            public Mesh mesh;
            public bool hasRequestedMesh;
            public bool hasMesh;
            private int lod;

            public LODMesh(int lod)
            {
                this.lod = lod;
            }

            void OnMeshDataReceived(MeshData meshData)
            {
                mesh = meshData.CreateMesh();
                hasMesh = true;
            }

            public void RequestMesh(MapData mapData)
            {
                hasRequestedMesh = true;
                _mapGenerator.RequestMeshData(OnMeshDataReceived, lod, mapData);
            }
        }

        [System.Serializable]
        public struct LODInfo
        {
            public int lod;
            public float visibleDistanceThreshold;
        }
    }
}