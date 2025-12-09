using UnityEngine;

namespace StarterAssets
{
    public class UICanvasControllerInput : MonoBehaviour
    {

        [Header("Output")]
        public StarterAssetsInputs starterAssetsInputs;

        public void VirtualMoveInput(Vector2 virtualMoveDirection)
        {
            starterAssetsInputs.MoveInput(virtualMoveDirection);
        }

        public void VirtualLookInput(Vector2 virtualLookDirection)
        {
            starterAssetsInputs.LookInput(virtualLookDirection);
        }

        public void VirtualJumpInput(bool virtualJumpState)
        {
            starterAssetsInputs.JumpInput(virtualJumpState);
        }

        public void VirtualSprintInput(bool virtualSprintState)
        {
            starterAssetsInputs.SprintInput(virtualSprintState);
        }



        // public void VirtualPrimaryWeaponInput()
        // {
        //     starterAssetsInputs.PrimaryWeaponInput();
        // }

        // public void VirtualSecondaryWeaponInput()
        // {
        //     starterAssetsInputs.SecondaryWeaponInput();
        // }
        // public void VirtualPistolInput()
        // {
        //     starterAssetsInputs.PistolInput();
        // }
        // public void VirtualShootInput()
        // {
        //     starterAssetsInputs.ShootInput();
        // }
        // public void VirtualAttckInput()
        // {
        //     starterAssetsInputs.AttackInput();
        // }
        // public void VirtualSearchInput()
        // {
        //     starterAssetsInputs.SearchInput();
        // }
        // public void VirtualReloadInput()
        // {
        //     starterAssetsInputs.ReloadInput();
        // }
        // public void VirtualBackpackInput()
        // {
        //     starterAssetsInputs.BackpackInput();
        // }
        // public void VirtualSettingInput()
        // {
        //     starterAssetsInputs.SettingInput();
        // }


    }
}