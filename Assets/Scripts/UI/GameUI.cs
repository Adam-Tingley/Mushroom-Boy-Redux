using UnityEngine;

public class GameUI : MonoBehaviour
{
    [SerializeField] private TimingUI _timeUI;
    [SerializeField] private LevelEndUI _endUI;
    
    public void UpdateTimes(RunState runState)
    {
        _timeUI.UpdateTimes(runState);
    }

    public void OnWorldChange(int newWorld, PlayerState playerState)
    {
        _timeUI.OnWorldChange(newWorld, playerState);
    }

    public void OnGameComplete(RunState run, PlayerState player)
    {
        _endUI.OnGameComplete(run, player);
    }
}
