using System;
using System.Collections.Generic;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    [SerializeField] private int _worldCount = 3;
    [SerializeField] private RunState _runState;
    [SerializeField] private PlayerState _playerState = new();
    [SerializeField] private GameUI _gameUI;
    [SerializeField] private LevelManager _levelManager;
    
    private bool _isActive = true;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _playerState.LoadFromPrefs();

        _runState.CurrentTimes = new float[_worldCount];
        _runState.BestTimes = new float[_worldCount];
        _runState.RunTime = 0;
        _runState.CurrentWorld = 0;

        for (int i = 0; i < _worldCount; i++)
        {
            if (_playerState.BestRunTimes.Count > i)
            {
                _runState.BestTimes[i] = _playerState.BestRunTimes[i] / 10f;
            }
        }
        
        _gameUI.OnWorldChange(0, _playerState);
    }

    // Update is called once per frame
    void Update()
    {
        if (_isActive)
            IncrementTimes();
        
        _gameUI.UpdateTimes(_runState);
    }

    private void IncrementTimes()
    {
        if (_runState.CurrentWorld < _runState.CurrentTimes.Length)
        {
            _runState.RunTime += Time.deltaTime;
            _runState.CurrentTimes[_runState.CurrentWorld] += Time.deltaTime;
        }
    }

    public void ResetRun()
    {
        Start();
    }
    
    public void OnLevelComplete()
    {
        _runState.CurrentWorld ++;
        _gameUI.OnWorldChange(_runState.CurrentWorld, _playerState);
        _levelManager.OnNewLevelReached(_runState.CurrentWorld);
    }
    
    public void SavePlayerProgress()
    {
        Debug.Log(_playerState.BestOverallTime);
        _playerState.SaveToPrefs();
    }
    
    public void WipePlayerProgress()
    {
        _playerState.Wipe();
    }
    
}
