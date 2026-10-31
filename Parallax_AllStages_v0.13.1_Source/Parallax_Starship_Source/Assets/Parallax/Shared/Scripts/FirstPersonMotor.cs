using UnityEngine;

namespace ShadeLink
{
    // The player transform is at the feet. Look rotation stays with the owning game.
    public sealed class FirstPersonMotor
    {
        public const float Speed = 4f;
        public const float Gravity = 20f;
        public const float JumpHeight = .9f;
        public const float AdditionalJumpHeight = .5f;
        public const float GroundAcceleration = 56.6f;
        public const float AirAcceleration = 15f;
        public const float AxisRate = 3f;
        const float JumpSpeed = 6f;
        const float JumpBuffer = .05f;
        const float JumpCooldown = .75f;
        const float TerminalSpeed = 20f;
        const float MantleDuration = .3f;

        Vector2 keyboardAxes;
        RaycastHit[] groundHits = new RaycastHit[32];
        Vector3 velocity;
        float verticalVelocity;
        float jumpBuffer = -1f;
        float jumpCooldown;
        float holdRemaining;
        bool mantleAvailable;
        float mantleElapsed;
        Vector3 mantleStart, mantleTarget;

        public Vector3 Velocity { get { return velocity; } }
        public bool Grounded { get; private set; }
        public bool Mantling { get; private set; }
        public bool SmoothKeyboardInput { get; set; } = true;
        public System.Func<Collider, bool> GroundFilter { get; set; }
        public float VerticalVelocity
        {
            get { return verticalVelocity; }
            set { verticalVelocity = value; velocity.y = value; }
        }

        public static void Configure(CharacterController controller, Camera camera)
        {
            controller.height = 1.6f;
            controller.radius = .3f;
            controller.center = Vector3.up * .8f;
            controller.stepOffset = .2f;
            controller.skinWidth = .05f;
            controller.minMoveDistance = 0;
            controller.slopeLimit = 60;
            if (camera == null) return;
            camera.transform.localPosition = Vector3.up * 1.55f;
            camera.fieldOfView = 70;
            camera.nearClipPlane = .05f;
        }

        public static Vector2 FilterKeyboard(Vector2 current, Vector2 raw, float dt)
        {
            return new Vector2(FilterAxis(current.x, raw.x, dt), FilterAxis(current.y, raw.y, dt));
        }

        static float FilterAxis(float current, float raw, float dt)
        {
            raw = Mathf.Clamp(raw, -1, 1);
            if (current * raw < 0) current = 0;
            return Mathf.MoveTowards(current, raw, AxisRate * Mathf.Max(0, dt));
        }

        public static Vector2 ShapeInput(Vector2 input)
        {
            float length = input.magnitude;
            if (length < .00001f) return Vector2.zero;
            float magnitude = Mathf.Min(1, length);
            return input / length * (magnitude * magnitude);
        }

        public void Reset()
        {
            keyboardAxes = Vector2.zero;
            velocity = Vector3.zero;
            verticalVelocity = 0;
            Grounded = false;
            Mantling = false;
            mantleAvailable = false;
            mantleElapsed = 0;
            jumpBuffer = -1;
            jumpCooldown = 0;
            holdRemaining = 0;
        }

        public void ClearInput()
        {
            keyboardAxes=Vector2.zero;
            velocity=new Vector3(0,verticalVelocity,0);
            jumpBuffer=-1;
            holdRemaining=0;
        }

        public void Step(CharacterController controller, Vector2 localInput, float yawRadians,
            bool jumpPressed, bool jumpHeld, float dt, int mask = ~0, Vector3 supportVelocity = default)
        {
            if (controller == null || !controller.enabled || dt <= 0) return;
            jumpCooldown = Mathf.Max(0, jumpCooldown - dt);
            if (jumpPressed) jumpBuffer = JumpBuffer;
            else jumpBuffer -= dt;

            keyboardAxes = SmoothKeyboardInput ? FilterKeyboard(keyboardAxes, localInput, dt) : localInput;
            Vector2 shaped = ShapeInput(keyboardAxes);
            float sin = Mathf.Sin(yawRadians), cos = Mathf.Cos(yawRadians);
            var forward = new Vector3(sin, 0, cos);
            var target = new Vector3(shaped.x * cos + shaped.y * sin, 0, shaped.y * cos - shaped.x * sin) * Speed;

            if (Mantling)
            {
                AdvanceMantle(controller, dt, mask);
                return;
            }

            // Probe now instead of trusting last frame's isGrounded: no coyote jump after a ledge/teleport.
            Grounded = verticalVelocity <= 0 && FindGround(controller, controller.transform.position, .09f, mask, out _);
            if (Grounded) verticalVelocity = 0;
            if (Grounded && jumpBuffer >= -.00001f && jumpCooldown <= .00001f)
            {
                verticalVelocity = JumpSpeed;
                holdRemaining = AdditionalJumpHeight / JumpSpeed;
                jumpBuffer = -1;
                jumpCooldown = JumpCooldown;
                mantleAvailable = true;
                Grounded = false;
            }
            else if (Grounded)
            {
                mantleAvailable = false;
            }

            var planar = new Vector3(velocity.x, 0, velocity.z);
            planar = Vector3.MoveTowards(planar, target, (Grounded ? GroundAcceleration : AirAcceleration) * dt);
            Vector3 horizontal = (planar + new Vector3(supportVelocity.x, 0, supportVelocity.z)) * dt;
            float verticalMove;
            bool adhere = Grounded && FindGround(controller, controller.transform.position + horizontal,
                Mathf.Max(controller.stepOffset, horizontal.magnitude) + .03f, mask, out _);
            if (adhere)
            {
                verticalMove = -Mathf.Max(controller.stepOffset, horizontal.magnitude);
            }
            else
            {
                float cancelled = 0;
                if (jumpHeld && verticalVelocity > 0 && holdRemaining > 0)
                {
                    cancelled = Mathf.Min(dt, holdRemaining);
                    holdRemaining -= cancelled;
                }
                else holdRemaining = 0;
                float gravityTime = dt - cancelled;
                verticalMove = verticalVelocity * dt - .5f * Gravity * gravityTime * gravityTime;
                float nextVertical = Mathf.Max(-TerminalSpeed, verticalVelocity - Gravity * gravityTime);
                if (verticalVelocity <= -TerminalSpeed) verticalMove = -TerminalSpeed * dt;
                verticalVelocity = nextVertical;
            }

            Vector3 start = controller.transform.position;
            CollisionFlags flags = controller.Move(horizontal + Vector3.up * (verticalMove + supportVelocity.y * dt));
            Vector3 actual = (controller.transform.position - start) / dt - supportVelocity;
            // Keep the realised tangent after collision. Projecting it back onto
            // the blocked diagonal would repeatedly damp wall sliding at high FPS.
            planar=Vector3.ClampMagnitude(new Vector3(actual.x,0,actual.z),planar.magnitude);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0)
            {
                verticalVelocity = 0;
                holdRemaining = 0;
            }
            Grounded = verticalVelocity <= 0 && FindGround(controller, controller.transform.position, .075f, mask, out _);
            if (Grounded) verticalVelocity = 0;
            velocity = new Vector3(planar.x, verticalVelocity, planar.z);

            if (!Grounded && mantleAvailable && localInput.y > .1f && verticalVelocity < -.1f)
                TryBeginMantle(controller, forward, mask);
        }

        bool FindGround(CharacterController controller, Vector3 feet, float distance, int mask, out RaycastHit ground)
        {
            float radius = Mathf.Max(.02f, controller.radius - .025f);
            Vector3 bottom = feet + controller.center - Vector3.up * (controller.height * .5f - controller.radius);
            int count;
            while(true)
            {
                count=Physics.SphereCastNonAlloc(bottom+Vector3.up*.05f,radius,Vector3.down,groundHits,
                    distance+.075f,mask,QueryTriggerInteraction.Ignore);
                if(count<groundHits.Length)break;
                if(groundHits.Length>=4096)
                {
                    groundHits=Physics.SphereCastAll(bottom+Vector3.up*.05f,radius,Vector3.down,
                        distance+.075f,mask,QueryTriggerInteraction.Ignore);
                    count=groundHits.Length;break;
                }
                System.Array.Resize(ref groundHits,groundHits.Length*2);
            }
            float nearest = float.PositiveInfinity;
            ground = default;
            for(int i=0;i<count;i++)
            {
                var hit=groundHits[i];
                if (IsSelf(controller, hit.collider) || !AcceptGround(hit.collider) ||
                    hit.normal.y < Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad)) continue;
                if (hit.point.y > feet.y + .1f || hit.distance >= nearest) continue;
                nearest = hit.distance; ground = hit;
            }
            return nearest < float.PositiveInfinity;
        }

        void TryBeginMantle(CharacterController controller, Vector3 forward, int mask)
        {
            Vector3 feet = controller.transform.position;
            // A reachable front face plus a walkable top prevents pulling onto unrelated floors behind a wall.
            if (!Ray(controller, feet + Vector3.up * .2f, forward, .8f, mask, out var face)) return;
            if (Mathf.Abs(face.normal.y) > .4f) return;
            Vector3 overEdge = face.point + forward * (controller.radius + .15f);
            if (!Ray(controller, new Vector3(overEdge.x, feet.y + 1.3f, overEdge.z), Vector3.down, 1.3f,
                mask, out var top)) return;
            if (!AcceptGround(top.collider)) return;
            float rise = top.point.y - feet.y;
            if (rise < .05f || rise > 1.15f || top.normal.y < Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad)) return;
            Vector3 destination = new Vector3(overEdge.x, top.point.y + .025f, overEdge.z);
            if (!CapsuleClear(controller, destination, mask)) return;
            // First lift clear of the edge, then cross it. Both legs retain normal controller collision.
            Vector3 lifted = new Vector3(feet.x, destination.y + .04f, feet.z);
            if (!SweepClear(controller, feet, lifted, mask) || !SweepClear(controller, lifted,
                destination + Vector3.up * .04f, mask)) return;
            mantleStart = feet;
            mantleTarget = destination;
            mantleElapsed = 0;
            Mantling = true;
            mantleAvailable = false;
            jumpBuffer = -1;
            holdRemaining = 0;
            verticalVelocity = 0;
            velocity = Vector3.zero;
        }

        void AdvanceMantle(CharacterController controller, float dt, int mask)
        {
            mantleElapsed += dt;
            float progress = Mathf.Clamp01(mantleElapsed / MantleDuration);
            Vector3 lifted = new Vector3(mantleStart.x, mantleTarget.y + .04f, mantleStart.z);
            Vector3 waypoint = progress < .5f ? Vector3.Lerp(mantleStart, lifted, progress * 2) :
                Vector3.Lerp(lifted, mantleTarget + Vector3.up * .04f, (progress - .5f) * 2);
            Vector3 start = controller.transform.position;
            Vector3 delta = waypoint - start;
            CollisionFlags flags = controller.Move(delta);
            velocity = (controller.transform.position - start) / dt;
            // A prop/door may enter the route after detection. Stop safely rather than teleporting through it.
            if ((flags & (CollisionFlags.Above | CollisionFlags.Sides)) != 0 &&
                (controller.transform.position - waypoint).sqrMagnitude > .0036f)
            {
                Mantling = false;
                verticalVelocity = Mathf.Min(0, velocity.y);
                return;
            }
            if (progress < 1) return;
            controller.Move(Vector3.down * .08f);
            Mantling = false;
            Grounded = FindGround(controller, controller.transform.position, .09f, mask, out _);
            verticalVelocity = 0;
            velocity = Vector3.zero;
        }

        static bool IsSelf(CharacterController controller, Collider collider)
        {
            return collider == controller || collider.transform.IsChildOf(controller.transform);
        }

        bool AcceptGround(Collider collider) { return GroundFilter == null || GroundFilter(collider); }

        static bool Ray(CharacterController controller, Vector3 origin, Vector3 direction, float distance,
            int mask, out RaycastHit nearest)
        {
            nearest = default;
            float closest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(origin, direction, distance, mask, QueryTriggerInteraction.Ignore))
            {
                if (IsSelf(controller, hit.collider) || hit.distance >= closest) continue;
                closest = hit.distance; nearest = hit;
            }
            return closest < float.PositiveInfinity;
        }

        static void Capsule(CharacterController controller, Vector3 feet, out Vector3 bottom, out Vector3 top, out float radius)
        {
            radius = Mathf.Max(.02f, controller.radius - .025f);
            Vector3 centre = feet + controller.center;
            float offset = Mathf.Max(0, controller.height * .5f - controller.radius);
            bottom = centre - Vector3.up * offset;
            top = centre + Vector3.up * offset;
        }

        static bool CapsuleClear(CharacterController controller, Vector3 feet, int mask)
        {
            Capsule(controller, feet, out var bottom, out var top, out float radius);
            foreach (var collider in Physics.OverlapCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore))
                if (!IsSelf(controller, collider)) return false;
            return true;
        }

        static bool SweepClear(CharacterController controller, Vector3 start, Vector3 end, int mask)
        {
            Vector3 delta = end - start;
            if (delta.sqrMagnitude < .000001f) return true;
            Capsule(controller, start, out var bottom, out var top, out float radius);
            foreach (var hit in Physics.CapsuleCastAll(bottom, top, radius, delta.normalized, delta.magnitude,
                mask, QueryTriggerInteraction.Ignore))
                if (!IsSelf(controller, hit.collider)) return false;
            return true;
        }
    }
}
