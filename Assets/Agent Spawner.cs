using UnityEngine;

public class DollSpawner : MonoBehaviour
{
    public GameObject dollPrefab;
    public int dollCount = 4;
    public Vector3 spawnAreaCenter;
    public float spacing = 3f;

    void Start()
    {
        for (int i = 0; i < dollCount; i++)
        {
            Vector3 offset = new Vector3((i % 2) * spacing, 0, (i / 2) * spacing);
            Vector3 spawnPos = spawnAreaCenter + offset;
            Instantiate(dollPrefab, spawnPos, Quaternion.identity);
        }
    }
}