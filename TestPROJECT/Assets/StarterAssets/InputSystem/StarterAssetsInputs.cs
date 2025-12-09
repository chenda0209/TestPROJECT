using System;
using UnityEngine;
using UnityEngine.Events;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;

		public bool jump;
		public bool sprint;

		public bool primaryWeapon;
		public bool secondaryWeapon;
		public bool pistol;
		public bool shoot;
		public bool attack;
		public bool search;
		public bool reload;
		public bool backpack;
		public bool setting;


		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;


#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if (cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
			// GameEventSystem.OnMoveInput?.Invoke(move);
		}

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
			// GameEventSystem.OnLookInput?.Invoke(look);
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}


		// public void PrimaryWeaponInput() { GameEventSystem.OnPrimaryWeaponInput?.Invoke(); Debug.Log("PrimaryWeaponInput"); }
		// public void SecondaryWeaponInput() { GameEventSystem.OnSecondaryWeaponInput?.Invoke(); Debug.Log("SecondaryWeaponInput"); }
		// public void PistolInput() { GameEventSystem.OnPistolInput?.Invoke(); Debug.Log("PistolInput"); }
		// public void ShootInput() { GameEventSystem.OnShootInput?.Invoke(); Debug.Log("ShootInput"); }
		// public void AttackInput() { GameEventSystem.OnAttackInput?.Invoke(); Debug.Log("AttackInput"); }
		// public void SearchInput() { GameEventSystem.OnSearchInput?.Invoke(); Debug.Log("SearchInput"); }
		// public void ReloadInput() { GameEventSystem.OnReloadInput?.Invoke(); Debug.Log("ReloadInput"); }
		// public void BackpackInput() { GameEventSystem.OnBackpackInput?.Invoke(); Debug.Log("BackpackInput"); }
		// public void SettingInput() { GameEventSystem.OnSettingInput?.Invoke(); Debug.Log("SettingInput"); }



		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}

    }

}