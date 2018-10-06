﻿using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu]
public class World : ScriptableObject
{
    public Chunk[,,] Chunks;
    public Block[,,] Blocks;
    public WorldGeneratorStatus Status { get; private set; }

    public byte ChunkSize = 32;
    public byte WorldSizeX = 7;
    public byte WorldSizeY = 4;
    public byte WorldSizeZ = 7;
    public float ChunkTerrainToGenerate { get; private set; }
    public float ChunkObjectsToGenerate { get; private set; }
    public float AlreadyGenerated { get; private set; }
    public string ProgressDescription;
    
    [SerializeField] TerrainGenerator _terrainGenerator;
    [SerializeField] MeshGenerator _meshGenerator;
    [SerializeField] Material _terrainTexture;
    [SerializeField] Material _waterTexture;

    Stopwatch _stopwatch = new Stopwatch();
    Scene _worldScene;
    long _accumulatedTerrainGenerationTime, _accumulatedMeshCreationTime;

    int _totalBlockNumberX, _totalBlockNumberY, _totalBlockNumberZ;
    int _progressStep = 1;

    void OnEnable()
    {
        _terrainGenerator = new TerrainGenerator(ChunkSize, WorldSizeX, WorldSizeY, WorldSizeZ);
        _meshGenerator = new MeshGenerator(ChunkSize, WorldSizeX, WorldSizeY, WorldSizeZ);
        
        ChunkObjectsToGenerate = WorldSizeX * WorldSizeY * WorldSizeZ;
        while (8 * _progressStep * 1.33f < ChunkObjectsToGenerate)
            _progressStep++;
        ChunkTerrainToGenerate = 8 * _progressStep;

        _totalBlockNumberX = WorldSizeX * ChunkSize;
        _totalBlockNumberY = WorldSizeY * ChunkSize;
        _totalBlockNumberZ = WorldSizeZ * ChunkSize;
    }

    /// <summary>
    /// Generates block types with hp and hp level. 
    /// Chunks and their objects (if first run = true). 
    /// And calculates faces.
    /// </summary>
    public IEnumerator GenerateWorld(bool firstRun)
    {
        _stopwatch.Restart();
        
        Status = WorldGeneratorStatus.GeneratingTerrain;

        yield return null;
        ProgressDescription = "Initialization";
        Blocks = new Block[WorldSizeX * ChunkSize, WorldSizeY * ChunkSize, WorldSizeZ * ChunkSize];
        AlreadyGenerated += _progressStep;

        yield return null;
        ProgressDescription = "Calculating heights";
        var heights = _terrainGenerator.CalculateHeights();
        AlreadyGenerated += _progressStep;

        yield return null;
        ProgressDescription = "Generating terrain";
        var types = _terrainGenerator.CalculateBlockTypes(heights);
        AlreadyGenerated += _progressStep;

        yield return null;
        ProgressDescription = "Output deflattenization";
        DeflattenizeOutput(ref types);
        AlreadyGenerated += _progressStep;

        yield return null;
        ProgressDescription = "Generating trees";
        _terrainGenerator.AddTrees(ref Blocks);
        AlreadyGenerated += _progressStep;

        yield return null;
        ProgressDescription = "Creating game objects";
        if (firstRun)
            _worldScene = SceneManager.CreateScene(name);

        // creating game objects
        Chunks = new Chunk[WorldSizeX, WorldSizeY, WorldSizeZ];
        CreateGameObjects(firstRun);
        AlreadyGenerated += _progressStep;

        yield return null;
        ProgressDescription = "Calculating faces";
        _meshGenerator.CalculateFaces(ref Blocks);
        AlreadyGenerated += _progressStep;

        yield return null;
        ProgressDescription = "World boundaries check";
        _meshGenerator.WorldBoundariesCheck(ref Blocks);
        AlreadyGenerated += _progressStep;

        Status = WorldGeneratorStatus.TerrainReady;
        
        _stopwatch.Stop();
        UnityEngine.Debug.Log($"It took {_stopwatch.ElapsedTicks / TimeSpan.TicksPerMillisecond} ms to generate all terrain.");
    }

    void DeflattenizeOutput(ref BlockTypes[] types)
    {
        for (var x = 0; x < _totalBlockNumberX; x++)
        {
            for (var y = 0; y < _totalBlockNumberY; y++)
                for (var z = 0; z < _totalBlockNumberZ; z++)
                {
                    var type = types[Utils.IndexFlattenizer3D(x, y, z, _totalBlockNumberX, _totalBlockNumberY)];
                    Blocks[x, y, z].Type = type;
                    Blocks[x, y, z].Hp = LookupTables.BlockHealthMax[(int)type];
                }
        }
    }

    void CreateGameObjects(bool firstRun)
    {
        for (int x = 0; x < WorldSizeX; x++)
            for (int z = 0; z < WorldSizeZ; z++)
                for (int y = 0; y < WorldSizeY; y++)
                {
                    var chunkCoord = new Vector3Int(x, y, z);

                    var c = new Chunk()
                    {
                        Position = new Vector3Int(chunkCoord.x * ChunkSize, chunkCoord.y * ChunkSize, chunkCoord.z * ChunkSize),
                        Coord = chunkCoord
                    };

                    if (firstRun)
                    {
                        CreateGameObjects(c);

                        SceneManager.MoveGameObjectToScene(c.Terrain.gameObject, _worldScene);
                        SceneManager.MoveGameObjectToScene(c.Water.gameObject, _worldScene);
                    }

                    Chunks[x, y, z] = c;
                }
    }

    public IEnumerator RedrawChunksIfNecessaryAsync()
    {
        _stopwatch.Restart();
        Status = WorldGeneratorStatus.GeneratingMeshes;

        for (int x = 0; x < WorldSizeX; x++)
            for (int z = 0; z < WorldSizeZ; z++)
                for (int y = 0; y < WorldSizeY; y++)
                {
                    Chunk c = Chunks[x, y, z];
                    if (c.Status == ChunkStatus.NeedToBeRecreated)
                        RecreateMeshAndCollider(c);
                    else if (c.Status == ChunkStatus.NeedToBeRedrawn) // used only for cracks
                        RecreateTerrainMesh(c);

                    AlreadyGenerated ++;

                    yield return null; // give back control
                }

        Status = WorldGeneratorStatus.AllReady;
        _stopwatch.Stop();
        _accumulatedMeshCreationTime += _stopwatch.ElapsedMilliseconds;
        UnityEngine.Debug.Log($"It took {_accumulatedTerrainGenerationTime} ms to redraw all meshes.");
    }

    public void RedrawChunksIfNecessary()
    {
        for (int x = 0; x < WorldSizeX; x++)
            for (int z = 0; z < WorldSizeZ; z++)
                for (int y = 0; y < WorldSizeY; y++)
                {
                    Chunk c = Chunks[x, y, z];
                    if (c.Status == ChunkStatus.NeedToBeRecreated)
                        RecreateMeshAndCollider(c);
                    else if (c.Status == ChunkStatus.NeedToBeRedrawn) // used only for cracks
                        RecreateTerrainMesh(c);
                }
    }

    public IEnumerator LoadWorld(SaveGameData save, bool firstRun)
    {
        _accumulatedTerrainGenerationTime = 0;
        _stopwatch.Restart();
        Status = WorldGeneratorStatus.GeneratingTerrain;
        AlreadyGenerated = 0;

        if (firstRun)
            _worldScene = SceneManager.CreateScene(name);

        for (int x = 0; x < WorldSizeX; x++)
            for (int z = 0; z < WorldSizeZ; z++)
                for (int y = 0; y < WorldSizeY; y++)
                {
                    var loaded = save.Chunks[x, y, z];
                    var c = new Chunk()
                    {
                        Coord = loaded.Coord,
                        Position = loaded.Position,
                        Status = ChunkStatus.NeedToBeRedrawn
                    };

                    if (firstRun)
                    {
                        CreateGameObjects(c);

                        SceneManager.MoveGameObjectToScene(c.Terrain.gameObject, _worldScene);
                        SceneManager.MoveGameObjectToScene(c.Water.gameObject, _worldScene);
                    }

                    AlreadyGenerated++;

                    yield return null; // give back control
                }

        Status = WorldGeneratorStatus.TerrainReady;
        _stopwatch.Stop();
        _accumulatedTerrainGenerationTime += _stopwatch.ElapsedMilliseconds;
        UnityEngine.Debug.Log($"It took {_accumulatedTerrainGenerationTime} ms to load all terrain.");
    }

    public IEnumerator GenerateMeshes()
    {
        ProgressDescription = "Generating meshes";

        _accumulatedMeshCreationTime = 0;
        _stopwatch.Restart();
        Status = WorldGeneratorStatus.GeneratingMeshes;

        for (int x = 0; x < WorldSizeX; x++)
            for (int z = 0; z < WorldSizeZ; z++)
                for (int y = 0; y < WorldSizeY; y++)
                {
                    var c = Chunks[x, y, z];
                    MeshData terrainData, waterData;
                    _meshGenerator.ExtractMeshData(ref Blocks, ref c.Position, out terrainData, out waterData);
                    CreateRenderingComponents(c, terrainData, waterData);
                    c.Status = ChunkStatus.Created;

                    AlreadyGenerated ++;

                    yield return null; // give back control
                }

        Status = WorldGeneratorStatus.AllReady;
        _stopwatch.Stop();
        _accumulatedMeshCreationTime += _stopwatch.ElapsedMilliseconds;
        ProgressDescription = "Ready";

        UnityEngine.Debug.Log("It took "
            + _accumulatedMeshCreationTime
            + " ms to create all meshes.");
    }

    void RecreateMeshAndCollider(Chunk c)
    {
        DestroyImmediate(c.Terrain.GetComponent<Collider>());

        MeshData t, w;
        _meshGenerator.ExtractMeshData(ref Blocks, ref c.Position, out t, out w);
        var tm = _meshGenerator.CreateMesh(t);
        var wm = _meshGenerator.CreateMesh(w);

        var terrainFilter = c.Terrain.GetComponent<MeshFilter>();
        terrainFilter.mesh = tm;
        var collider = c.Terrain.gameObject.AddComponent(typeof(MeshCollider)) as MeshCollider;
        collider.sharedMesh = tm;

        var waterFilter = c.Water.GetComponent<MeshFilter>();
        waterFilter.mesh = wm;

        c.Status = ChunkStatus.Created;
    }

    /// <summary>
    /// Destroys terrain mesh and recreates it.
    /// Used for cracks as they do not change the terrain geometry.
    /// </summary>
    void RecreateTerrainMesh(Chunk c)
    {
        MeshData t, w;
        _meshGenerator.ExtractMeshData(ref Blocks, ref c.Position, out t, out w);
        var tm = _meshGenerator.CreateMesh(t);

        var meshFilter = c.Terrain.GetComponent<MeshFilter>();
        meshFilter.mesh = tm;

        c.Status = ChunkStatus.Created;
    }

    void CreateGameObjects(Chunk c)
    {
        string name = "" + c.Coord.x + c.Coord.y + c.Coord.z;
        c.Terrain = new GameObject(name + "_terrain");
        c.Terrain.transform.position = c.Position;
        c.Water = new GameObject(name + "_water");
        c.Water.transform.position = c.Position;
        c.Status = ChunkStatus.NeedToBeRedrawn;
    }

    void CreateRenderingComponents(Chunk chunk, MeshData terrainData, MeshData waterData)
    {
        var meshT = _meshGenerator.CreateMesh(terrainData);
        var rt = chunk.Terrain.gameObject.AddComponent(typeof(MeshRenderer)) as MeshRenderer;
        rt.material = _terrainTexture;

        var mft = (MeshFilter)chunk.Terrain.AddComponent(typeof(MeshFilter));
        mft.mesh = meshT;

        var ct = chunk.Terrain.gameObject.AddComponent(typeof(MeshCollider)) as MeshCollider;
        ct.sharedMesh = meshT;

        var meshW = _meshGenerator.CreateMesh(waterData);
        var rw = chunk.Water.gameObject.AddComponent(typeof(MeshRenderer)) as MeshRenderer;
        rw.material = _waterTexture;

        var mfw = (MeshFilter)chunk.Water.AddComponent(typeof(MeshFilter));
        mfw.mesh = meshW;
    }
}
