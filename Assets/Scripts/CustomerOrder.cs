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

        int day = GameEngine.CurrentDay; // <--- USE GAME ENGINE

        // -------------------------
        // DAY 1 – ONLY TEA, NO ADDITIONS
        // -------------------------
        if (day == 1)
        {
            order.cupType = CupType.Tea;

            TeaType[] teas = { TeaType.Red, TeaType.Green, TeaType.Black, TeaType.Blue };
            order.teaType = teas[Random.Range(0, teas.Length)];

            order.milk = false;
            order.honey = false;
            order.ice = false;
            return;
        }

        // -------------------------
        // DAY 2 – TEA ONLY, ADD MILK/HONEY, NO ICE
        // -------------------------
        if (day == 2)
        {
            order.cupType = CupType.Tea;

            TeaType[] teas = { TeaType.Red, TeaType.Green, TeaType.Black, TeaType.Blue };
            order.teaType = teas[Random.Range(0, teas.Length)];

            order.milk = (Random.value < chanceMilk);
            order.honey = (Random.value < chanceHoney);
            order.ice = false; // no ice on day 2
            return;
        }

        // -------------------------
        // DAY 3 – FULL SYSTEM: GLASS, ICE, ADDITIONS
        // -------------------------
        if (day == 3)
        {
            // Random cup
            order.cupType = (Random.value < 0.5f) ? CupType.Tea : CupType.Glass;

            // Random tea
            TeaType[] teas = { TeaType.Red, TeaType.Green, TeaType.Black, TeaType.Blue };
            order.teaType = teas[Random.Range(0, teas.Length)];

            // Extras
            order.milk = (Random.value < chanceMilk);
            order.honey = (Random.value < chanceHoney);
            order.ice = (Random.value < chanceIce);

            // Logical cleanup
            if (order.ice && order.cupType == CupType.Tea)
                order.ice = false;

            return;
        }

        // Fallback if something unexpected happens
        order.cupType = CupType.Tea;
        order.teaType = TeaType.Red;
        order.milk = false;
        order.honey = false;
        order.ice = false;
    }

    public string GetOrderString() => order.ToString();
}
