using UnityEngine;
namespace PixelOcean
{
    [RequireComponent(typeof(SpriteRenderer),typeof(CircleCollider2D))]
    public sealed class SodaCanProjectile : MonoBehaviour
    {
        private Vector2 velocity; private float gravity=5.2f, life=5f; private bool bounced;
        public void Launch(Vector2 start, SharkLaneSwimmer target, Sprite sprite, float direction)
        {
            transform.position = start;
            GetComponent<SpriteRenderer>().sprite = sprite;
            transform.localScale = Vector3.one * .325f;

            CircleCollider2D c=GetComponent<CircleCollider2D>(); c.isTrigger=true;c.radius=.22f;
            Vector2 aim=target!=null?(Vector2)target.transform.position:start+Vector2.right*direction*4f;
            float miss=Random.value<.28f?Random.Range(-.85f,.85f):Random.Range(-.16f,.16f); aim+=new Vector2(miss,Random.Range(-.12f,.18f));
            float t=Mathf.Clamp(Vector2.Distance(start,aim)/5.5f,.38f,.85f);
            velocity=new Vector2((aim.x-start.x)/t,(aim.y-start.y+.5f*gravity*t*t)/t);
        }
        private void Update(){ life-=Time.deltaTime;if(life<=0){Destroy(gameObject);return;} velocity.y-=gravity*Time.deltaTime;transform.position+=(Vector3)(velocity*Time.deltaTime);transform.Rotate(0,0,-760f*Time.deltaTime); }
        private void OnTriggerEnter2D(Collider2D other)
        {
            SharkLaneSwimmer shark=other.GetComponentInParent<SharkLaneSwimmer>();
            if(shark!=null){ shark.TakeSodaCanHit(transform.position); Bounce(shark.transform.position); return; }
            if(!bounced) Bounce(other.transform.position);
        }
        private void Bounce(Vector2 from){bounced=true;Vector2 away=((Vector2)transform.position-from).normalized;if(away.sqrMagnitude<.1f)away=Vector2.up;velocity=away*2.2f+Vector2.up*1.5f;gravity=7f;life=Mathf.Min(life,1.75f);GetComponent<CircleCollider2D>().enabled=false;}
    }
}
