using System.Reflection;
using System.Text.Json;
using Silk.NET.Maths;
using TheAdventure.Models;
using TheAdventure.Models.Data;
using TheAdventure.Scripting;

namespace TheAdventure;

public class Engine
{
    private readonly GameRenderer _renderer;
    private readonly Input _input;
    private readonly ScriptEngine _scriptEngine = new();
    private int _highScore = 0;
    private double _timeSinceLastHeart = 0;
private readonly double _heartSpawnInterval = 10000; 

    private readonly Dictionary<int, GameObject> _gameObjects = new();
    private readonly Dictionary<string, TileSet> _loadedTileSets = new();
    private readonly Dictionary<int, Tile> _tileIdMap = new();

    private Level _currentLevel = new();
    private PlayerObject? _player;
    private int _score = 0;
    private bool _isGameOver = false;



    private DateTimeOffset _lastUpdate = DateTimeOffset.Now;

   public Engine(GameRenderer renderer, Input input)
{
    _renderer = renderer;
    _input = input;

    _input.OnMouseClick += (_, coords) =>
    {
        int resetX = 5;
        int resetY = 30;
        int resetWidth = 120;
        int resetHeight = 40;

        if (coords.x >= resetX && coords.x <= resetX + resetWidth &&
            coords.y >= resetY && coords.y <= resetY + resetHeight)
        {
            Console.WriteLine(">>> CLICK pe RESET");
            RestartGame();
        }
        else
        {
            AddBomb(coords.x, coords.y);
        }
    };
}


    public void SetupWorld()
    {
        _gameObjects.Clear();
        _tileIdMap.Clear();
        _loadedTileSets.Clear();

        _player = new(SpriteSheet.Load(_renderer, "Player.json", "Assets"), 100, 100);
        _player = new(SpriteSheet.Load(_renderer, "Player.json", "Assets"), 100, 100);
        

        var levelContent = File.ReadAllText(Path.Combine("Assets", "terrain.tmj"));
        var level = JsonSerializer.Deserialize<Level>(levelContent);
        if (level == null)
        {
            throw new Exception("Failed to load level");
        }

        foreach (var tileSetRef in level.TileSets)
        {
            var tileSetContent = File.ReadAllText(Path.Combine("Assets", tileSetRef.Source));
            var tileSet = JsonSerializer.Deserialize<TileSet>(tileSetContent);
            if (tileSet == null)
            {
                throw new Exception("Failed to load tile set");
            }

            foreach (var tile in tileSet.Tiles)
            {
                tile.TextureId = _renderer.LoadTexture(Path.Combine("Assets", tile.Image), out _);
                _tileIdMap.Add(tile.Id!.Value, tile);
            }

            _loadedTileSets.Add(tileSet.Name, tileSet);
        }

        if (level.Width == null || level.Height == null)
        {
            throw new Exception("Invalid level dimensions");
        }

        if (level.TileWidth == null || level.TileHeight == null)
        {
            throw new Exception("Invalid tile dimensions");
        }

        _renderer.SetWorldBounds(new Rectangle<int>(0, 0, level.Width.Value * level.TileWidth.Value,
            level.Height.Value * level.TileHeight.Value));

        _currentLevel = level;

        _scriptEngine.LoadAll(Path.Combine("Assets", "Scripts"));
        AddHeart(400, 300); 
        _renderer.LoadRestartButton();
        _renderer.LoadGameOverImage();
        _isGameOver = false;
        AudioManager.Init();


    }

    public void ProcessFrame()
    {
        var currentTime = DateTimeOffset.Now;
        var msSinceLastFrame = (currentTime - _lastUpdate).TotalMilliseconds;
        _lastUpdate = currentTime;

        if (_player == null)
        {
            return;
        }

        double up = _input.IsUpPressed() ? 1.0 : 0.0;
        double down = _input.IsDownPressed() ? 1.0 : 0.0;
        double left = _input.IsLeftPressed() ? 1.0 : 0.0;
        double right = _input.IsRightPressed() ? 1.0 : 0.0;
        bool isAttacking = _input.IsKeyAPressed() && (up + down + left + right <= 1);
        bool addBomb = _input.IsKeyBPressed();

        _player.UpdatePosition(up, down, left, right, 48, 48, msSinceLastFrame);
        if (isAttacking)
        {
            _player.Attack();
        }

        _scriptEngine.ExecuteAll(this);

        if (addBomb)
        {
            AddBomb(_player.Position.X, _player.Position.Y, false);
        }
        
        
_timeSinceLastHeart += msSinceLastFrame;
if (_timeSinceLastHeart >= _heartSpawnInterval)
{
    _timeSinceLastHeart = 0;
    var rand = new Random();
    int x = rand.Next(100, 800); 
    int y = rand.Next(100, 600);
    AddHeart(x, y);
}


    }

    public void RenderFrame()
    {
        _renderer.SetDrawColor(0, 0, 0, 255);
        _renderer.ClearScreen();

        var playerPosition = _player!.Position;
        _renderer.CameraLookAt(playerPosition.X, playerPosition.Y);

        RenderTerrain();
        RenderAllObjects();
        _renderer.RenderTextCrossPlatform($"Score: {_score}", 20, 20);
        _renderer.RenderTextCrossPlatform($"High Score: {_highScore}", 20, 50);


        _renderer.RenderRestartButton();
      if (_isGameOver)
        {
            _renderer.RenderGameOverImage();
            _renderer.RenderTextCrossPlatform($"Final Score: {_score}", 20, 80);
        }

        _renderer.PresentFrame();
    }
public void RenderAllObjects()
{
    var toRemove = new List<int>();

    foreach (var gameObject in GetRenderables())
    {
        gameObject.Render(_renderer);

     if (gameObject is RenderableGameObject heart && heart.SpriteSheet != null &&
    heart.SpriteSheet.FrameWidth == 32 &&
    heart.SpriteSheet.FrameHeight == 32 &&
    _player != null)


        {
            var deltaX = Math.Abs(_player.Position.X - gameObject.Position.X);
            var deltaY = Math.Abs(_player.Position.Y - gameObject.Position.Y);

            if (deltaX < 32 && deltaY < 32)
            {
                _player.GainLife(); 
                toRemove.Add(gameObject.Id);
            }
        }

        // 💣 Verificare bombă temporară
        if (gameObject is TemporaryGameObject { IsExpired: true } tempGameObject)
        {
            toRemove.Add(tempGameObject.Id);
        }
    }

    foreach (var id in toRemove)
    {
        _gameObjects.Remove(id, out var gameObject);

        if (_player == null)
            continue;

        if (gameObject is TemporaryGameObject tempGameObject)
        {
            var deltaX = Math.Abs(_player.Position.X - tempGameObject.Position.X);
            var deltaY = Math.Abs(_player.Position.Y - tempGameObject.Position.Y);

            if (deltaX < 32 && deltaY < 32)
            {
                _player.LoseLife();
            }
            else
            {
                _score += 10;
            }

            AudioManager.PlayExplosion();

            if (_player.Lives <= 0)
            {
                _isGameOver = true;
            }
        }
    }

    _player?.Render(_renderer);

    if (_player != null)
    {
        _renderer.DrawLivesWithImage(_player.Lives);
    }

    if (_player != null && _player.Lives <= 0)
    {
        _isGameOver = true;

        if (_score > _highScore)
        {
            _highScore = _score;
        }
    }
}


    public void RenderTerrain()
    {
        foreach (var currentLayer in _currentLevel.Layers)
        {
            for (int i = 0; i < _currentLevel.Width; ++i)
            {
                for (int j = 0; j < _currentLevel.Height; ++j)
                {
                    int? dataIndex = j * currentLayer.Width + i;
                    if (dataIndex == null)
                    {
                        continue;
                    }

                    var currentTileId = currentLayer.Data[dataIndex.Value] - 1;
                    if (currentTileId == null)
                    {
                        continue;
                    }

                    var currentTile = _tileIdMap[currentTileId.Value];

                    var tileWidth = currentTile.ImageWidth ?? 0;
                    var tileHeight = currentTile.ImageHeight ?? 0;

                    var sourceRect = new Rectangle<int>(0, 0, tileWidth, tileHeight);
                    var destRect = new Rectangle<int>(i * tileWidth, j * tileHeight, tileWidth, tileHeight);
                    _renderer.RenderTexture(currentTile.TextureId, sourceRect, destRect);
                }
            }
        }
    }

    public IEnumerable<RenderableGameObject> GetRenderables()
    {
        foreach (var gameObject in _gameObjects.Values)
        {
            if (gameObject is RenderableGameObject renderableGameObject)
            {
                yield return renderableGameObject;
            }
        }
    }

    public (int X, int Y) GetPlayerPosition()
    {
        return _player!.Position;
    }

    public void AddBomb(int X, int Y, bool translateCoordinates = true)
    {
        var worldCoords = translateCoordinates ? _renderer.ToWorldCoordinates(X, Y) : new Vector2D<int>(X, Y);

        SpriteSheet spriteSheet = SpriteSheet.Load(_renderer, "BombExploding.json", "Assets");
        spriteSheet.ActivateAnimation("Explode");

        TemporaryGameObject bomb = new(spriteSheet, 2.1, (worldCoords.X, worldCoords.Y));
        _gameObjects.Add(bomb.Id, bomb);
    }
    public void RestartGame()
    {
        _gameObjects.Clear();
        _score = 0;
        _isGameOver = false;
        SetupWorld();
    }
public void AddHeart(int x, int y, bool translateCoordinates = true)
{
    var worldCoords = translateCoordinates ? _renderer.ToWorldCoordinates(x, y) : new Vector2D<int>(x, y);

    var spriteSheet = SpriteSheet.Load(_renderer, "Heart.json", "Assets");
    spriteSheet.ActivateAnimation("Heart"); // ← AICI activezi animația

    RenderableGameObject heart = new(spriteSheet, (worldCoords.X, worldCoords.Y));
    _gameObjects.Add(heart.Id, heart);
}



}