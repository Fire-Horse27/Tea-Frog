using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GrabTea : MonoBehaviour
{
    public GameObject teaObject;
    public GameObject teaObject2;
    public GameObject teaObject3;
    public GameObject teaObject4;

    private void OnTriggerEnter2D(Collider2D other)
    {
        //Debug.Log("Trigger Entered");

        if (other.CompareTag("Player"))
        {
            //Debug.Log("Found the Player");
            teaObject2.SetActive(false); // removes currently held tea bag
            teaObject3.SetActive(false);
            teaObject4.SetActive(false);
            teaObject.SetActive(true); // adds newly selected tea bag

        }
    }
}
