using UnityEngine;

namespace Hollowknight.Silksong
{
    // Turns the hero's art to the way the hero faces. The art is on a child so flipping never touches the collider.
    // Same rule as HeroSceneBootstrap.LateUpdate, for a hero whose art was added by RoomWorldStarter.
    public class HeroArtFlip : MonoBehaviour
    {
        HeroController hero;
        SpriteRenderer sprite;
        bool artFacesLeft;

        public void Configure(HeroController owner, SpriteRenderer renderer, bool sourceArtFacesLeft)
        {
            hero = owner;
            sprite = renderer;
            artFacesLeft = sourceArtFacesLeft;
        }

        void LateUpdate()
        {
            if (hero == null || sprite == null) return;
            sprite.flipX = artFacesLeft ? hero.FacingRight : !hero.FacingRight;
        }
    }
}
