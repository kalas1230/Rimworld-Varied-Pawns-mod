using System;
using Verse;

namespace PawnVarianceMod
{
    public class Dialog_RenameProfile : Dialog_Rename<CustomProfile>
    {
        private readonly Action onRenamed;

        public Dialog_RenameProfile(CustomProfile profile, Action onRenamed) : base(profile)
        {
            this.onRenamed = onRenamed;
        }

        protected override void OnRenamed(string name)
        {
            onRenamed?.Invoke();
        }
    }
}
