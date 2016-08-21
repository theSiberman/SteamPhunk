using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using CSML;
using TerrainStitch;
using System.IO;


[System.Serializable]
public class TerrainTile
{
    public GameObject terrain;
    public Texture2D heightmap;
	public Texture2D diffuseMap;
//    public string url;
    public int x;
    public int y;
    public int z;

    public int worldX;
    public int worldZ;


    public TerrainTile(int tilex, int tiley, int wldz, int wldx)  
    {
        x = tilex;
        y = tiley;
        z = 5;              // Hardcoded zoom level
        worldX = wldx;
        worldZ = wldz;
    }
}

public class TextureUrls {
	public string altimiterUrl;
	public string diffuseUrl;
}

public class TerrainLoader : MonoBehaviour {
	//This is where available planets are entered, in the enum declaration
	//The next line declares a public Planet called planet which is exposed in the editor
	public enum Planet{MARS,VESTA};
	public Planet planet;

	public int tileSize = 256;
	public int terrainSize = 256;
	public int terrainResolution = 2048;
	public int terrainHeight = 100;

	public int tileMargin;
	public int startX;
	public int startY;

	public bool updateCache = false;
	public bool flatTerrain = false;

	public Dictionary<string, TerrainTile> worldTiles = new Dictionary<string, TerrainTile>();


	//This switches the urls based on the selected planet, any new planet added to the enum will
	//need a case here populated with the relevant links, otherwise it will revert to the default case
	//and load the Mars textures
	private TextureUrls urls (Planet planet) {
		switch(planet){
		case Planet.MARS:
			TextureUrls marsUrls = new TextureUrls();
			//DEM Grayscale - Mars Orbiter Laser Altimeter
			marsUrls.altimiterUrl = "https://api.nasa.gov/mars-wmts/catalog/Mars_MGS_MOLA_DEM_mosaic_global_463m_8/1.0.0//default/default028mm/";
			//Atlas Mosaic - Mars Orbiter Camera
			marsUrls.diffuseUrl = "https://api.nasa.gov/mars-wmts/catalog/msss_atlas_simp_clon/1.0.0//default/default028mm/";
			return marsUrls;

		case Planet.VESTA:
			TextureUrls vestaUrls = new TextureUrls();
			//DEM Grayscale - Mars Orbiter Laser Altimeter
			vestaUrls.altimiterUrl = "https://api.nasa.gov/mars-wmts/catalog/Mars_MGS_MOLA_DEM_mosaic_global_463m_8/1.0.0//default/default028mm/";
			//Atlas Mosaic - Mars Orbiter Camera
			vestaUrls.diffuseUrl = "https://api.nasa.gov/mars-wmts/catalog/msss_atlas_simp_clon/1.0.0//default/default028mm/";
			return vestaUrls;

			default:
			TextureUrls urls = new TextureUrls();
			//DEM Grayscale - Mars Orbiter Laser Altimeter
			urls.altimiterUrl = "https://api.nasa.gov/mars-wmts/catalog/Mars_MGS_MOLA_DEM_mosaic_global_463m_8/1.0.0//default/default028mm/";
			//Atlas Mosaic - Mars Orbiter Camera
			urls.diffuseUrl = "https://api.nasa.gov/mars-wmts/catalog/msss_atlas_simp_clon/1.0.0//default/default028mm/";
			return urls;
		}
	}

	//Check image cache for tile, download and add if not existing, else use cache if updateCache flag not set.
	private Texture2D cachedOrWebTexture (Planet planet, string type, string tileImagePath, string url) {
		Texture2D texture = new Texture2D(terrainResolution, terrainResolution);
		string filePath = Application.persistentDataPath + "/" + planet.ToString() + "_" + type + "_" + tileImagePath.Replace("/","_");

		//Get the cached file
		if( System.IO.File.Exists( filePath ) ){
			texture.LoadImage(File.ReadAllBytes( filePath ));
		} else {
			// Download the tile texture of type = type using the urls returned from the 'urls' method which switches on the
			// planet passed to it, in this case, the one selected in the inspector ( the public planet )
			WWW www = new WWW(url + tileImagePath);
			while (!www.isDone) { }
			www.LoadImageIntoTexture(texture);
			System.IO.File.WriteAllBytes(filePath, texture.EncodeToPNG());
		}
		return texture;
	}

    IEnumerator loadTerrainTile(TerrainTile tile)
    {
        // Create and position GameObject
        var terrainData = new TerrainData();
        terrainData.heightmapResolution = terrainResolution;
        terrainData.alphamapResolution = tileSize;

		string tileImagePath = tile.z + "/" + tile.x + "/" + tile.y + ".png";

		tile.heightmap = cachedOrWebTexture( planet, "heightmap", tileImagePath, urls(planet).altimiterUrl );
		tile.diffuseMap = cachedOrWebTexture( planet, "diffusemap", tileImagePath, urls(planet).diffuseUrl );

        // Multidimensional array of this tiles heights in x/y
        float[,] terrainHeights = terrainData.GetHeights(0, 0, terrainResolution + 1, terrainResolution + 1);

        // Load altimiter colors into byte array
		Color[] altimiterPixelByteArray = tile.heightmap.GetPixels();

		// Load diffuse colors into byte array
		Color[] diffusePixelByteArray = tile.heightmap.GetPixels();

        if (flatTerrain)
        {
            for (int y = 0; y <= tileSize; y++)
            {
                for (int x = 0; x <= tileSize; x++)
                {
                    terrainHeights[y, x] = 0f;
                }
            }
        }
        else
        {
			//This function seems to be scaling the altimiter resolution up to the desired texture resolution
            for (int y = 0; y <= terrainResolution; y++)
            {
                for (int x = 0; x <= terrainResolution; x++)
                {
                    if (x == terrainResolution && y == terrainResolution)
                    {
						terrainHeights[y, x] = altimiterPixelByteArray[(y - 1) * tileSize + (x - 1)].grayscale;
                    }
                    else if (x == terrainResolution)
                    {
						terrainHeights[y, x] = altimiterPixelByteArray[(y) * tileSize + (x - 1)].grayscale;
                    }
                    else if (y == terrainResolution)
                    {
						terrainHeights[y, x] = altimiterPixelByteArray[((y - 1) * tileSize) + x].grayscale;
                    }
                    else
                    {
						terrainHeights[y, x] = altimiterPixelByteArray[y * tileSize + x].grayscale;
                    }
                }
            }
        }
        
        // Use the newly populated height data to apply the heightmap
        terrainData.SetHeights(0, 0, terrainHeights);

        // Set terrain size
        terrainData.size = new Vector3(terrainSize, terrainHeight, terrainSize);

        tile.terrain = Terrain.CreateTerrainGameObject(terrainData);
        tile.terrain.transform.position = new Vector3(tile.worldX * terrainSize, 0, tile.worldZ * terrainSize);

        tile.terrain.name = "tile_" + tile.x.ToString() + "_" + tile.y.ToString();

        yield return null;
    }

    void loadAllTerrain()
    {
        
        foreach(TerrainTile tile in worldTiles.Values)
        {
            StartCoroutine(loadTerrainTile(tile));
        }
    }

    void loadTilesAround(int z, int x, int margin)
    {
        for(int tilex = x - margin; tilex <= x + margin; tilex++)
        {
            for (int tilez = z - margin; tilez <= z + margin; tilez++)
            {
                worldTiles[tilex.ToString() + "_" + tilez.ToString()] = new TerrainTile(
                    tilez, tilex, z - tilez, -(x - tilex));
            }
        }
    }
    
    // Use this for initialization
    void Start()
    {
        loadTilesAround(startX, startY, tileMargin);

        // Initial tile loading
        loadAllTerrain();

        TerrainStitchEditor t = new TerrainStitchEditor();
        t.StitchTerrain();

		Debug.Log(worldTiles.Count);
		Debug.Log(worldTiles.Values);

        foreach(TerrainTile tile in worldTiles.Values)
        {
//			Debug.Log("tile" + tile);
//			TerrainTextures _texture = GetComponent<TerrainTextures>();
//			Terrain _terrain = tile.terrain.GetComponent<Terrain>();
//			TerrainData _data = _terrain.terrainData;
//			_texture.setTextures(_data);
			GetComponent<TerrainTextures>().setTextures(tile.terrain.GetComponent<Terrain>().terrainData, tile.diffuseMap, terrainSize);
        }
        
    }
}