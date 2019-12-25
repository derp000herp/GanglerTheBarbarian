using System;
using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;

namespace MyAssets.Characters.ThirdPerson
{
    [RequireComponent(typeof(ThirdPersonCharacter))]
    public class ThirdPersonUserControl : MonoBehaviour
    {
        [SerializeField] float m_DoubleTapSpeed = 0.5f;


        private ThirdPersonCharacter m_Character; // A reference to the ThirdPersonCharacter on the object
        private Transform m_Cam;                  // A reference to the main camera in the scenes transform
        private Vector3 m_CamForward;             // The current forward direction of the camera
        private Vector3 m_Move;                   // the world-relative desired move direction, calculated from the camForward and user input.
        private float m_lastTapTime;
        private bool m_Jump;
        private ThirdPersonCharacter.QuickMovement quickMovement;
        private ThirdPersonCharacter.AttackType attackType;


        private void Start()
        {
            // get the transform of the main camera
            if (Camera.main != null)
            {
                m_Cam = Camera.main.transform;
            }
            else
            {
                Debug.LogWarning(
                    "Warning: no main camera found. Third person character needs a Camera tagged \"MainCamera\", for camera-relative controls.", gameObject);
                // we use self-relative controls in this case, which probably isn't what the user wants, but hey, we warned them!
            }

            // get the third person character ( this should never be null due to require component )
            m_Character = GetComponent<ThirdPersonCharacter>();
            quickMovement = ThirdPersonCharacter.QuickMovement.None;
            attackType = ThirdPersonCharacter.AttackType.None;
            m_lastTapTime = 0;
        }


        private void Update()
        {
            // jump
            if (!m_Jump)
            {
                m_Jump = CrossPlatformInputManager.GetButtonDown("Jump");
            }
            // quick movement
            if (quickMovement == ThirdPersonCharacter.QuickMovement.None)
            {
                if (CrossPlatformInputManager.GetButtonDown("RollForward"))
                {
                    quickMovement = ThirdPersonCharacter.QuickMovement.RollForward;
                }
                else if (CrossPlatformInputManager.GetButtonDown("RollBackward"))
                {
                    quickMovement = ThirdPersonCharacter.QuickMovement.RollBackward;
                }
                else if (CrossPlatformInputManager.GetButtonDown("RollLeft"))
                {
                    quickMovement = ThirdPersonCharacter.QuickMovement.RollLeft;
                }
                else if (CrossPlatformInputManager.GetButtonDown("RollRight"))
                {
                    quickMovement = ThirdPersonCharacter.QuickMovement.RollRight;
                }
            }
            // attacking
            if (attackType == ThirdPersonCharacter.AttackType.None)
            {
                if (CrossPlatformInputManager.GetButtonDown("LightAttack"))
                {
                    attackType = ThirdPersonCharacter.AttackType.Light;
                }
                else if (CrossPlatformInputManager.GetButtonDown("HeavyAttack"))
                {
                    attackType = ThirdPersonCharacter.AttackType.Heavy;
                }
            }
        }


        // Fixed update is called in sync with physics
        private void FixedUpdate()
        {
            // read inputs
            float h = CrossPlatformInputManager.GetAxis("Horizontal");
            float v = CrossPlatformInputManager.GetAxis("Vertical");
            bool crouch = CrossPlatformInputManager.GetButton("Crouch");

            // calculate move direction to pass to character
            if (m_Cam != null)
            {
                // calculate camera relative direction to move:
                m_CamForward = Vector3.Scale(m_Cam.forward, new Vector3(1, 0, 1)).normalized;
                m_Move = v * m_CamForward + h * m_Cam.right;
            }
            else
            {
                // we use world-relative directions in the case of no main camera
                m_Move = v * Vector3.forward + h * Vector3.right;
            }
#if !MOBILE_INPUT
            // Sprint speed multiplier
            if (CrossPlatformInputManager.GetButton("Sprint")) m_Move *= 0.5f;
#endif
            // delete - for testing only
            if (Input.GetKey(KeyCode.O)) m_Character.GetHit();
            if (Input.GetKey(KeyCode.N)) m_Character.Die();
            if (Input.GetKey(KeyCode.M)) m_Character.Revive();
            // pass all parameters to the character control script
            m_Character.Move(m_Move, crouch, m_Jump, quickMovement);
            m_Character.Attack(attackType);
            m_Jump = false;
            quickMovement = ThirdPersonCharacter.QuickMovement.None;
            attackType = ThirdPersonCharacter.AttackType.None;
        }
    }
}
