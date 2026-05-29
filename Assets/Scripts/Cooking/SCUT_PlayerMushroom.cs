using UnityEngine;

public class SCUT_PlayerMushroom : MonoBehaviour
{
    public int count = 0;
    public KeyCode pickupKey = KeyCode.Space;

    void Update()
    {
        if (Input.GetKeyDown(pickupKey))
        {
            count++;
            Debug.Log("Mushroom Picked! Count: " + count + "/5");

            if (count >= 5)
            {
                TriggerImpLevel();
            }
        }
    }

    private void TriggerImpLevel()
    {
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go.activeInHierarchy)
            {
                go.SendMessage("TriggerImpAttack", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}