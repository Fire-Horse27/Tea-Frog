using UnityEngine;

[RequireComponent(typeof(FrogAI))]
public class CustomerOrder : MonoBehaviour
{
    public OrderData order;

    [Header("Randomization")]
    public float chanceMilk = 0.35f;
    public float chanceHoney = 0.25f;
    public float chanceIce = 0.2f;

    void Start()
    {
        GenerateRandomOrder();
    }

    public void GenerateRandomOrder()
    {
        order = new OrderData();

        // Randomly pick a cup
        order.cupType = (Random.value < 0.5f) ? CupType.Tea : CupType.Glass;

        // Pick one of the four tea colors
        order.teaType = (TeaType)Random.Range(0, 4); // 0=Red, 1=Green, 2=Black, 3=Blue

        // Extras
        order.milk = (Random.value < chanceMilk);
        order.honey = (Random.value < chanceHoney);
        order.ice = (Random.value < chanceIce);

        // Logical cleanup
        if (order.ice && order.cupType == CupType.Tea)
            order.ice = false; // no ice in mugs
        if (order.ice && order.milk)
            order.milk = false; // iced + milk conflict
    }

    public string GetOrderString() => order.ToString();
}
