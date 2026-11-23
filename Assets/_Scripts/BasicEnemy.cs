using UnityEngine;

public class BasicEnemy : MonoBehaviour
{
	[SerializeField] int Health = 5;
	[SerializeField] float attackRadius = 2;
	[SerializeField] LayerMask playerLayer;
	[SerializeField] Animator enemyAnimator;


	private void Update()
	{
		checkAttackable();
	}

	void checkAttackable()
	{
		Collider2D col = Physics2D.OverlapCircle(transform.position, attackRadius, playerLayer);

		if (col != null)
		{
			enemyAnimator.SetBool("isAttaking", true);
		}
		else
		{
			enemyAnimator.SetBool("isAttaking", false);
		}
	}

	public void getDamage(int damage=1)
	{
		Health -= damage;
		enemyAnimator.SetTrigger("Hurt");
		if (Health <= 0)
		{
			Die();
		}
	}

	void Die()
	{
		enemyAnimator.SetTrigger("Hurt");
		Destroy(this.gameObject);
	}
}
