using UnityEngine;

[RequireComponent(typeof(FrogAI))]
public class CustomerOrder : MonoBehaviour
{
    public OrderData order;

    [Header("Randomization options")]
    public string[] teaColors = new string[] { "red tea", "blue tea", "green tea", "black tea" };
    public float chanceMilk = 0.35f;
    public float chanceHoney = 0.25f;
    public float chanceIce = 0.2f; // iced orders less common

    void Start()
    {
        GenerateRandomOrder();
    }

    public void GenerateRandomOrder()
    {
        order = new OrderData();

        // Cup type: randomly choose mug or glass (mug = "Tea")
        order.cupType = (Random.value < 0.5f) ? "Tea" : "Glass";

        // Tea color
        order.teaColor = teaColors[Random.Range(0, teaColors.Length)];

        // Extras
        order.milk = (Random.value < chanceMilk);
        order.honey = (Random.value < chanceHoney);

        // Ice is only sensible if cup is Glass, and conflicts with milk (optional)
        if (order.cupType == "Glass" && Random.value < chanceIce)
            order.ice = true;
        else
            order.ice = false;

        // If iced and milk is true, you may decide to disallow: let's prevent milk+ice by default
        if (order.ice && order.milk) order.milk = false;
    }

    public string GetOrderString()
    {
        return order.ToString();
    }
}
