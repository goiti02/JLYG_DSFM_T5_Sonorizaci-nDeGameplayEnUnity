using UnityEngine;

public class PickableItem : MonoBehaviour
{
	public int amount=1;
	private void OnTriggerEnter2D(Collider2D collision)
	{
		collision.GetComponent<PlayerController>().addPickable(amount);
		Destroy(this.gameObject);
	}
}
