using UnityEngine;

namespace SlashDemo
{
    // Persistent approach sectors plus soft separation, applied only while pursuing.
    public sealed class BearPackSteering : MonoBehaviour
    {
        SlashArena arena;
        SlashEnemy self;
        BearDemon navigation;
        Vector2 target, lastPlayer;
        float nextTarget;
        int lastCount = -1;
        bool openSpace = true;
        public Vector2 ApproachPoint => target;

        public void Initialize(SlashArena world, SlashEnemy owner, BearDemon nav)
        { arena = world; self = owner; navigation = nav; }

        bool Ally(SlashEnemy other)
            => other != null && other != self && other.rockBear && other.Alive && other.gameObject.activeInHierarchy;
        static Vector2 Position(SlashEnemy other) => other.GetComponent<Rigidbody2D>().position;

        public bool HasRoom(Vector2 from)
        {
            foreach(var other in arena.Enemies)
                if(Ally(other) && Vector2.Distance(from,Position(other)) < self.Radius+other.Radius+(openSpace?.65f:.20f)) return false;
            return true;
        }

        Vector2 Separation(Vector2 from)
        {
            Vector2 result=Vector2.zero;
            foreach(var other in arena.Enemies)
            {
                if(!Ally(other)) continue;
                Vector2 away=from-Position(other);
                float distance=away.magnitude;
                float gap=self.Radius+other.Radius+(openSpace?.95f:.35f);
                if(distance>=gap) continue;
                Vector2 direction=distance>.001f ? away/distance : DemoArt.Direction(navigation.FormationSlot*120+90);
                result+=direction*Mathf.Clamp01((gap-distance)/.9f);
            }
            return result;
        }

        Vector2 ChooseApproach(Vector2 player,float radius)
        {
            float preferred=150+navigation.FormationSlot*120;
            openSpace=true;
            for(int sample=0;sample<12;sample++)
                if(!navigation.Walkable(player+DemoArt.Direction(preferred+sample*30)*radius)) openSpace=false;
            Vector2 best=player;
            float bestScore=float.PositiveInfinity;
            // Choose a nearby valid sector when the preferred one lies outside the path.
            for(int sample=0;sample<12;sample++)
            {
                float angle=preferred+sample*30;
                Vector2 candidate=player+DemoArt.Direction(angle)*radius;
                if(!navigation.Walkable(candidate)) continue;
                float score=Mathf.Abs(Mathf.DeltaAngle(preferred,angle))/90;
                foreach(var other in arena.Enemies)
                    if(Ally(other)) score+=Mathf.Max(0,self.Radius+other.Radius+(openSpace?.9f:.35f)-Vector2.Distance(candidate,Position(other)))*2;
                if(score<bestScore) { bestScore=score; best=candidate; }
            }
            // The existing pathfinder handles very narrow passages with no usable sector.
            return best;
        }

        public Vector2 Velocity(Vector2 from,Vector2 player)
        {
            if(self.moveSpeed<=0) return Vector2.zero;
            int count=0;
            foreach(var other in arena.Enemies) if(Ally(other)) count++;
            if(count==0)
                return Vector2.Distance(from,player)>self.attackRange*.9f
                    ? navigation.ChaseDirection(from,player)*self.moveSpeed : Vector2.zero;

            float ring=Mathf.Max(self.Radius+arena.Player.GetComponent<CircleCollider2D>().radius+.35f,self.attackRange*.86f);
            nextTarget-=Time.fixedDeltaTime;
            if(nextTarget<=0 || (player-lastPlayer).sqrMagnitude>.25f || count!=lastCount)
            {
                target=ChooseApproach(player,ring); lastPlayer=player; lastCount=count; nextTarget=.30f;
            }
            Vector2 desired=navigation.ChaseDirection(from,target)*Mathf.Clamp01(Vector2.Distance(from,target)/.6f);
            Vector2 pathDirection=desired;
            Vector2 radial=from-player;
            float distance=radial.magnitude;
            Vector2 targetRadial=target-player;
            if(openSpace && targetRadial.sqrMagnitude>.1f && distance>.1f && distance<ring*2.2f)
            {
                float angle=Vector2.SignedAngle(radial,targetRadial);
                if(Mathf.Abs(angle)>40)
                {
                    // Circle toward the assigned flank instead of cutting through the player.
                    Vector2 outward=radial/distance;
                    Vector2 tangent=new Vector2(-outward.y,outward.x)*Mathf.Sign(angle);
                    desired=tangent+outward*Mathf.Clamp((ring-distance)*1.5f,-.65f,.8f);
                }
            }
            desired=Vector2.ClampMagnitude(desired+Separation(from)*3f,1);
            if(desired.sqrMagnitude<.0001f) return Vector2.zero;
            float lookAhead=.45f+self.moveSpeed*Time.fixedDeltaTime;
            if(!navigation.Clear(from,from+desired.normalized*lookAhead))
            {
                if(pathDirection.sqrMagnitude>.01f && navigation.Clear(from,from+pathDirection.normalized*lookAhead))
                    return pathDirection*self.moveSpeed;
                Vector2 original=desired;
                bool found=false;
                for(int step=1;step<=3 && !found;step++) for(int side=0;side<2;side++)
                {
                    float angle=step*30*(side==0?1:-1)*((navigation.FormationSlot%2)==0?1:-1)*Mathf.Deg2Rad;
                    Vector2 rotated=new Vector2(original.x*Mathf.Cos(angle)-original.y*Mathf.Sin(angle),original.x*Mathf.Sin(angle)+original.y*Mathf.Cos(angle));
                    if(!navigation.Clear(from,from+rotated.normalized*lookAhead)) continue;
                    desired=rotated; found=true; break;
                }
                if(!found) return Vector2.zero;
            }
            return desired*self.moveSpeed;
        }
    }
}
