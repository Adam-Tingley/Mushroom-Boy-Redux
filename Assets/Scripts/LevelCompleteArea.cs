using System;
using UnityEngine;

public class LevelCompleteArea : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log(other.gameObject);
    }
}
