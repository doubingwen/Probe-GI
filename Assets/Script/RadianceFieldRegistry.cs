using System.Collections.Generic;

namespace Dou.GI
{
    internal static class RadianceFieldRegistry
    {
        static readonly List<RadianceFieldVolume> ActiveVolumes = new List<RadianceFieldVolume>();

        internal static IReadOnlyList<RadianceFieldVolume> Volumes => ActiveVolumes;

        internal static RadianceFieldVolume PrimaryVolume
        {
            get
            {
                for (int index = 0; index < ActiveVolumes.Count; index++)
                {
                    RadianceFieldVolume volume = ActiveVolumes[index];
                    if (volume != null && volume.isActiveAndEnabled && volume.HasCoefficientHistory)
                        return volume;
                }

                return null;
            }
        }

        internal static void Register(RadianceFieldVolume volume)
        {
            if (volume != null && !ActiveVolumes.Contains(volume))
                ActiveVolumes.Add(volume);
        }

        internal static void Unregister(RadianceFieldVolume volume)
        {
            ActiveVolumes.Remove(volume);
        }
    }
}
