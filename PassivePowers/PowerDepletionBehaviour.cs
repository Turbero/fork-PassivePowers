using UnityEngine;

namespace PassivePowers
{
	public class PowerDepletionBehaviour : MonoBehaviour
	{
		public string statusEffect = null!;
		public void Awake()
		{
			if (Player.m_localPlayer)
			{
				Player.m_localPlayer.m_seman.AddStatusEffect(statusEffect);
				Destroy(gameObject);
			}
		}
	}
}
