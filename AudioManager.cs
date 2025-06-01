using System.Media;

public static class AudioManager
{
    private static SoundPlayer? _explosionPlayer;
    public static void Init()
    {
        _explosionPlayer = new SoundPlayer("Assets/explosion.wav");
        _explosionPlayer.Load();
    }

    public static void PlayExplosion()
    {
        _explosionPlayer?.Play();
    }
}