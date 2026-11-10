using System;

namespace UGS.State
{
    /// <summary>把多个行为组合成一个（按顺序转发）。</summary>
    public sealed class CompositeBehavior : StateBehavior
    {
        private readonly StateBehavior[] _behaviors;

        public CompositeBehavior(params StateBehavior[] behaviors)
        {
            _behaviors = behaviors ?? Array.Empty<StateBehavior>();
        }

        public override bool CanEnter(StateContext context)
        {
            for (int i = 0; i < _behaviors.Length; i++)
            {
                StateBehavior behavior = _behaviors[i];
                if (behavior != null && !behavior.CanEnter(context))
                {
                    return false;
                }
            }

            return true;
        }

        public override void OnEnter(StateContext context)
        {
            for (int i = 0; i < _behaviors.Length; i++)
            {
                if (_behaviors[i] != null)
                {
                    _behaviors[i].OnEnter(context);
                }
            }
        }

        public override void OnTick(StateContext context, float deltaTime)
        {
            for (int i = 0; i < _behaviors.Length; i++)
            {
                if (_behaviors[i] != null)
                {
                    _behaviors[i].OnTick(context, deltaTime);
                }
            }
        }

        public override void OnExit(StateContext context)
        {
            for (int i = _behaviors.Length - 1; i >= 0; i--)
            {
                if (_behaviors[i] != null)
                {
                    _behaviors[i].OnExit(context);
                }
            }
        }
    }
}
