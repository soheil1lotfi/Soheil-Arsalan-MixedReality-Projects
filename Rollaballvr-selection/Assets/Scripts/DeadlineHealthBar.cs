using UnityEngine;
using UnityEngine.UI;

public class DeadlineHealthBar : MonoBehaviour
{
    public float duration = 60f;
    private Vector3 startScale;
    private float t;

    void Start()
    {
        startScale = transform.localScale;
    }

    void Update()
    {
        t += Time.deltaTime / duration;
        float x = Mathf.Lerp(startScale.x, 0f, t);
        transform.localScale = new Vector3(x, startScale.y, startScale.z);
    }

}
