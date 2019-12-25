using UnityEngine;

namespace MyAssets.Characters.ThirdPerson
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Animator))]
    public class ThirdPersonCharacter : MonoBehaviour
    {
        public enum QuickMovement { None, RollForward, RollBackward, RollLeft, RollRight, DashLeft, DashRight }
        public enum AttackType { None, Light, Heavy }
        public enum Abilities { None, Ability1, Ability2, Ability3, Ability4 }

        [SerializeField] float m_MovingTurnSpeed = 360;
        [SerializeField] float m_StationaryTurnSpeed = 180;
        [SerializeField] float m_JumpSpeed = 6f;
        [SerializeField] float m_DashSpeed = 6f;
        [Range(1f, 4f)] [SerializeField] float m_GravityMultiplier = 2f;
        [SerializeField] float m_RunCycleLegOffset = 0.2f; //specific to the character in sample assets, will need to be modified to work with others
        [SerializeField] float m_MoveSpeedMultiplier = 1f;
        [SerializeField] float m_AnimSpeedMultiplier = 1f;
        [SerializeField] float m_GroundCheckDistance = 0.3f;
        [SerializeField] float m_HitRange = 2f;
        [SerializeField] float m_LightAttackDamage = 10f;
        [SerializeField] float m_HeavyAttackDamage = 30f;

        Rigidbody m_Rigidbody;
        Animator m_Animator;
        bool m_IsGrounded;
        float m_OrigGroundCheckDistance;
        const float k_Half = 0.5f;
        float m_TurnAmount;
        float m_ForwardAmount;
        Vector3 m_GroundNormal;
        float m_CapsuleHeight;
        Vector3 m_CapsuleCenter;
        CapsuleCollider m_Capsule;
        bool m_Crouching;
        QuickMovement m_QuickMoving;


        void Start()
        {
            m_Animator = GetComponent<Animator>();
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Capsule = GetComponent<CapsuleCollider>();
            m_CapsuleHeight = m_Capsule.height;
            m_CapsuleCenter = m_Capsule.center;

            m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            m_OrigGroundCheckDistance = m_GroundCheckDistance;
            m_QuickMoving = QuickMovement.None;
        }


        public void Move(Vector3 move, bool crouch, bool jump, QuickMovement quickMovement)
        {

            // convert the world relative moveInput vector into a local-relative
            // turn amount and forward amount required to head in the desired
            // direction.
            if (move.magnitude > 1f) move.Normalize();
            move = transform.InverseTransformDirection(move);
            CheckGroundStatus();
            move = Vector3.ProjectOnPlane(move, m_GroundNormal);
            m_TurnAmount = Mathf.Atan2(move.x, move.z);
            m_ForwardAmount = move.z;
            m_QuickMoving = quickMovement;

            ApplyExtraTurnRotation();

            // control and velocity handling is different when grounded and airborne:
            if (m_IsGrounded)
            {
                HandleGroundedMovement(crouch, jump, quickMovement);
            }
            else
            {
                HandleAirborneMovement(quickMovement);
            }

            ScaleCapsuleForCrouching(crouch);
            PreventStandingInLowHeadroom();

            // send input and other state parameters to the animator
            UpdateAnimator(move);
        }

        public void Attack(AttackType attackType)
        {
            if (attackType != AttackType.None)
            {
                RaycastHit hit;
                Vector3 forward = transform.TransformDirection(Vector3.forward);
                Vector3 origin = transform.position;
                float attackDamage = 0f;

                if (attackType == AttackType.Light)
                {
                    attackDamage = m_LightAttackDamage;
                    m_Animator.SetTrigger("LightAttack");
                }
                if (attackType == AttackType.Heavy)
                {
                    attackDamage = m_HeavyAttackDamage;
                    m_Animator.SetTrigger("HeavyAttack");
                }
                if (Physics.Raycast(origin, forward, out hit, m_HitRange))
                {
                    if (hit.transform.gameObject.tag == "Enemy")
                    {
                        hit.transform.gameObject.SendMessage("TakeDamage", attackDamage);
                    }
                }
            }
        }

        public void Ability(Abilities ability)
        {
            // implement
        }

        public void GetHit()
        {
            m_Animator.SetInteger("HitVariant", Random.Range(1, 6));
            m_Animator.SetTrigger("Hit");
        }

        public void Die()
        {
            m_Animator.SetBool("Dead", true);
        }

        public void Revive()
        {
            m_Animator.SetBool("Dead", false);
        }

        void ScaleCapsuleForCrouching(bool crouch)
        {
            if (m_IsGrounded && crouch)
            {
                if (m_Crouching) return;
                m_Capsule.height = m_Capsule.height / 2f;
                m_Capsule.center = m_Capsule.center / 2f;
                m_Crouching = true;
            }
            else
            {
                Ray crouchRay = new Ray(m_Rigidbody.position + Vector3.up * m_Capsule.radius * k_Half, Vector3.up);
                float crouchRayLength = m_CapsuleHeight - m_Capsule.radius * k_Half;
                if (Physics.SphereCast(crouchRay, m_Capsule.radius * k_Half, crouchRayLength, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    m_Crouching = true;
                    return;
                }
                m_Capsule.height = m_CapsuleHeight;
                m_Capsule.center = m_CapsuleCenter;
                m_Crouching = false;
            }
        }

        void PreventStandingInLowHeadroom()
        {
            // prevent standing up in crouch-only zones
            if (!m_Crouching)
            {
                Ray crouchRay = new Ray(m_Rigidbody.position + Vector3.up * m_Capsule.radius * k_Half, Vector3.up);
                float crouchRayLength = m_CapsuleHeight - m_Capsule.radius * k_Half;
                if (Physics.SphereCast(crouchRay, m_Capsule.radius * k_Half, crouchRayLength, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    m_Crouching = true;
                }
            }
        }


        void UpdateAnimator(Vector3 move)
        {
            // update the animator parameters
            m_Animator.SetFloat("Forward", m_ForwardAmount, 0.1f, Time.deltaTime);
            m_Animator.SetFloat("Turn", m_TurnAmount, 0.1f, Time.deltaTime);
            m_Animator.SetBool("Crouch", m_Crouching);
            m_Animator.SetBool("OnGround", m_IsGrounded);
            if (!m_IsGrounded)
            {
                m_Animator.SetFloat("Jump", m_Rigidbody.velocity.y);
            }
            //Debug.Log(m_QuickMoving);
            switch (m_QuickMoving)
            {
                case QuickMovement.None:
                    break;
                case QuickMovement.RollForward:
                    m_Animator.SetTrigger("RollForward");
                    break;
                case QuickMovement.RollBackward:
                    m_Animator.SetTrigger("RollBackward");
                    break;
                case QuickMovement.RollLeft:
                    m_Animator.SetTrigger("RollLeft");
                    break;
                case QuickMovement.RollRight:
                    m_Animator.SetTrigger("RollRight");
                    break;
                default:
                    break;
            }

            // calculate which leg is behind, so as to leave that leg trailing in the jump animation
            // (This code is reliant on the specific run cycle offset in our animations,
            // and assumes one leg passes the other at the normalized clip times of 0.0 and 0.5)
            float runCycle =
                Mathf.Repeat(
                    m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime + m_RunCycleLegOffset, 1);
            float jumpLeg = (runCycle < k_Half ? 1 : -1) * m_ForwardAmount;
            if (m_IsGrounded)
            {
                m_Animator.SetFloat("JumpLeg", jumpLeg);
            }

            // the anim speed multiplier allows the overall speed of walking/running to be tweaked in the inspector,
            // which affects the movement speed because of the root motion.
            if (m_IsGrounded && move.magnitude > 0)
            {
                m_Animator.speed = m_AnimSpeedMultiplier;
            }
            else
            {
                // don't use that while airborne
                m_Animator.speed = 1;
            }
        }


        void HandleAirborneMovement(QuickMovement quickMovement)
        {
            // apply extra gravity from multiplier:
            Vector3 extraGravityForce = (Physics.gravity * m_GravityMultiplier) - Physics.gravity;
            m_Rigidbody.AddForce(extraGravityForce);

            m_GroundCheckDistance = m_Rigidbody.velocity.y < 0 ? m_OrigGroundCheckDistance : 0.01f;

            if (quickMovement != QuickMovement.None && !m_Animator.GetCurrentAnimatorStateInfo(0).IsName("Airborne"))
            {
                if (quickMovement == QuickMovement.DashLeft)
                {
                    // implement
                }
                if (quickMovement == QuickMovement.DashRight)
                {
                    // implement
                }
            }
        }


        void HandleGroundedMovement(bool crouch, bool jump, QuickMovement quickMovement)
        {
            // check whether conditions are right to allow a jump:
            if (jump && !crouch && m_Animator.GetCurrentAnimatorStateInfo(0).IsName("Grounded"))
            {
                // jump!
                m_Rigidbody.velocity = new Vector3(m_Rigidbody.velocity.x, m_JumpSpeed, m_Rigidbody.velocity.z);
                m_IsGrounded = false;
                m_Animator.applyRootMotion = false;
                m_GroundCheckDistance = 0.1f;
            }

            //if (quickMovement != QuickMovement.None && !crouch && m_Animator.GetCurrentAnimatorStateInfo(0).IsName("Grounded"))
            //{
            //    switch (quickMovement)
            //    {
            //        case QuickMovement.RollForward:
            //            m_Rigidbody.velocity = new Vector3(m_Rigidbody.velocity.x, m_Rigidbody.velocity.y, m_RollSpeed);
            //            break;
            //        case QuickMovement.RollBackward:
            //            m_Rigidbody.velocity = new Vector3(m_Rigidbody.velocity.x, m_Rigidbody.velocity.y, -m_RollSpeed);
            //            break;
            //        case QuickMovement.RollLeft:
            //            m_Rigidbody.velocity = new Vector3(-m_RollSpeed, m_Rigidbody.velocity.y, m_Rigidbody.velocity.z);
            //            break;
            //        case QuickMovement.RollRight:
            //            m_Rigidbody.velocity = new Vector3(m_RollSpeed, m_Rigidbody.velocity.y, m_Rigidbody.velocity.z);
            //            break;
            //        default:
            //            break;
            //    }
            //}
        }

        void ApplyExtraTurnRotation()
        {
            // help the character turn faster (this is in addition to root rotation in the animation)
            float turnSpeed = Mathf.Lerp(m_StationaryTurnSpeed, m_MovingTurnSpeed, m_ForwardAmount);
            transform.Rotate(0, m_TurnAmount * turnSpeed * Time.deltaTime, 0);
        }


        public void OnAnimatorMove()
        {
            // we implement this function to override the default root motion.
            // this allows us to modify the positional speed before it's applied.
            if (m_IsGrounded && Time.deltaTime > 0)
            {
                Vector3 v = (m_Animator.deltaPosition * m_MoveSpeedMultiplier) / Time.deltaTime;

                // we preserve the existing y part of the current velocity.
                v.y = m_Rigidbody.velocity.y;
                m_Rigidbody.velocity = v;
            }
        }


        void CheckGroundStatus()
        {
            RaycastHit hitInfo;
#if UNITY_EDITOR
            // helper to visualise the ground check ray in the scene view
            Debug.DrawLine(transform.position + (Vector3.up * 0.1f), transform.position + (Vector3.up * 0.1f) + (Vector3.down * m_GroundCheckDistance));
#endif
            // 0.1f is a small offset to start the ray from inside the character
            // it is also good to note that the transform position in the sample assets is at the base of the character
            if (Physics.Raycast(transform.position + (Vector3.up * 0.1f), Vector3.down, out hitInfo, m_GroundCheckDistance))
            {
                m_GroundNormal = hitInfo.normal;
                m_IsGrounded = true;
                m_Animator.applyRootMotion = true;
            }
            else
            {
                m_IsGrounded = false;
                m_GroundNormal = Vector3.up;
                m_Animator.applyRootMotion = false;
            }
        }
    }
}
