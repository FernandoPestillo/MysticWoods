using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectionController : MonoBehaviour
{
    public string _tagTargetDetection = "Player";
    public GameObject parentGameObject;

    public List<Collider2D> detectedObjs = new List<Collider2D>();

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.name == _tagTargetDetection)
        {
            detectedObjs.Add(collision);
            if (parentGameObject)
            {
                parentGameObject.GetComponent<Animator>().SetBool("isMoving", true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.name == _tagTargetDetection)
        {
            detectedObjs.Remove(collision);
            if (parentGameObject)
            {
                parentGameObject.GetComponent<Animator>().SetBool("isMoving", false);
            }
        }
    }
}
