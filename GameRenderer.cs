using Silk.NET.Maths;
using Silk.NET.SDL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TheAdventure.Models;
using Point = Silk.NET.SDL.Point;


namespace TheAdventure;


public unsafe class GameRenderer
{

  private Sdl _sdl;
private Renderer* _renderer;
private GameWindow _window;

    private Camera _camera;

    private Dictionary<int, IntPtr> _texturePointers = new();
    private Dictionary<int, TextureData> _textureData = new();
    private int _textureId;

    public GameRenderer(Sdl sdl, GameWindow window)
    {
        _sdl = sdl;

        _renderer = (Renderer*)window.CreateRenderer();
        _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);

        _window = window;
        var windowSize = window.Size;
        _camera = new Camera(windowSize.Width, windowSize.Height);
    }
private int _restartTextureId;
private TextureData _restartTextureData;
private int _gameOverTextureId;
private TextureData _gameOverTextureData;
public void LoadGameOverImage()
{
    _gameOverTextureId = LoadTexture("Assets/gameover.png", out _gameOverTextureData);
}

public void LoadRestartButton()
{
    _restartTextureId = LoadTexture("Assets/restart.png", out _restartTextureData);
}


    public void SetWorldBounds(Rectangle<int> bounds)
    {
        _camera.SetWorldBounds(bounds);
    }

    public void CameraLookAt(int x, int y)
    {
        _camera.LookAt(x, y);
    }

    public int LoadTexture(string fileName, out TextureData textureInfo)
    {
        using (var fStream = new FileStream(fileName, FileMode.Open))
        {
            var image = Image.Load<Rgba32>(fStream);
            textureInfo = new TextureData()
            {
                Width = image.Width,
                Height = image.Height
            };
            var imageRAWData = new byte[textureInfo.Width * textureInfo.Height * 4];
            image.CopyPixelDataTo(imageRAWData.AsSpan());
            fixed (byte* data = imageRAWData)
            {
                var imageSurface = _sdl.CreateRGBSurfaceWithFormatFrom(data, textureInfo.Width,
                    textureInfo.Height, 8, textureInfo.Width * 4, (uint)PixelFormatEnum.Rgba32);
                if (imageSurface == null)
                {
                    throw new Exception("Failed to create surface from image data.");
                }

                var imageTexture = _sdl.CreateTextureFromSurface(_renderer, imageSurface);
                if (imageTexture == null)
                {
                    _sdl.FreeSurface(imageSurface);
                    throw new Exception("Failed to create texture from surface.");
                }

                _sdl.FreeSurface(imageSurface);

                _textureData[_textureId] = textureInfo;
                _texturePointers[_textureId] = (IntPtr)imageTexture;
            }
        }

        return _textureId++;
    }
    public void RenderText(string text, int x, int y)
    {
        Console.WriteLine($"[RenderText] {text} at ({x},{y})"); // momentan afișăm doar în consolă
    }


    public void RenderTexture(int textureId, Rectangle<int> src, Rectangle<int> dst,
        RendererFlip flip = RendererFlip.None, double angle = 0.0, Point center = default)
    {
        if (_texturePointers.TryGetValue(textureId, out var imageTexture))
        {
            var translatedDst = _camera.ToScreenCoordinates(dst);
            _sdl.RenderCopyEx(_renderer, (Texture*)imageTexture, in src,
                in translatedDst,
                angle,
                in center, flip);
        }
    }
    public void RenderTextureScreenSpace(int textureId, Rectangle<int> src, Rectangle<int> dest)
    {
        if (_texturePointers.TryGetValue(textureId, out var imageTexture))
        {
            _sdl.RenderCopy(_renderer, (Texture*)imageTexture, in src, in dest);
        }
    }
    public void DrawLivesWithImage(int lives, int startX = 10, int startY = 10)
    {
        int heartTextureId = LoadTexture("Assets/heart.png", out _);

        for (int i = 0; i < lives; i++)
        {
            // Micșorăm la 32x32 px și poziționăm sus-stânga
            var dest = new Rectangle<int>(startX + i * 36, startY, 32, 32);
            var src = new Rectangle<int>(0, 0, 48, 48); // imaginea ta e 48x48, dar noi o afișăm mai mică
            RenderTextureScreenSpace(heartTextureId, src, dest);
        }
    }

    public Vector2D<int> ToWorldCoordinates(int x, int y)
    {
        return _camera.ToWorldCoordinates(new Vector2D<int>(x, y));
    }

    public void SetDrawColor(byte r, byte g, byte b, byte a)
    {
        _sdl.SetRenderDrawColor(_renderer, r, g, b, a);
    }

    public void ClearScreen()
    {
        _sdl.RenderClear(_renderer);
    }

    public void PresentFrame()
    {
        _sdl.RenderPresent(_renderer);
    }
    public void RenderRestartButton()
    {
        var desiredWidth = 120;
        var desiredHeight = 40;

        var x = 5; // colț stânga
        var y = 30; // mai jos (sub inimioare și scor)

        var src = new Rectangle<int>(0, 0, _restartTextureData.Width, _restartTextureData.Height);
        var dest = new Rectangle<int>(x, y, desiredWidth, desiredHeight);

        RenderTextureScreenSpace(_restartTextureId, src, dest);
    }
public void RenderGameOverImage()
{
    var screenWidth = _window.Size.Width;
    var screenHeight = _window.Size.Height;

    var imageWidth = 300;
    var imageHeight = 100;

    var x = (screenWidth - imageWidth) / 2;
    var y = (screenHeight - imageHeight) / 2;

    var src = new Rectangle<int>(0, 0, _gameOverTextureData.Width, _gameOverTextureData.Height);
    var dest = new Rectangle<int>(x, y, imageWidth, imageHeight);

    RenderTextureScreenSpace(_gameOverTextureId, src, dest);
}

}

