using System;
using System.Collections.Generic;

namespace AntMe.Simulation
{
    internal partial class SimulationEnvironment
    {
        #region common stuff

        /// <summary>
        /// Holds the current playground.
        /// </summary>
        internal CorePlayground Playground;

        /// <summary>
        /// Holds a list of active teams.
        /// </summary>
        internal CoreTeam[] Teams;

        /// <summary>
        /// Holds the "colony" of bugs.
        /// </summary>
        internal CoreColony Bugs;

        #endregion


        #region angle-precalculation

        /// <summary>
        /// Holds the calculated sin- and cos-values.
        /// </summary>
        public static int[,] Cos, Sin;

        /// <summary>
        /// Calculates all possible angles.
        /// </summary>
        private static void precalculateAngles()
        {
            int max = SimulationSettings.Custom.MaximumSpeed * PLAYGROUND_UNIT + 1;

            Cos = new int[max + 1, 360];
            Sin = new int[max + 1, 360];

            // precalculation of cosinus and sinus
            for (int amplitude = 0; amplitude <= max; amplitude++)
            {
                for (int angle = 0; angle < 360; angle++)
                {
                    Cos[amplitude, angle] =
                      (int)Math.Round(amplitude * Math.Cos(angle * Math.PI / 180d));
                    Sin[amplitude, angle] =
                      (int)Math.Round(amplitude * Math.Sin(angle * Math.PI / 180d));
                }
            }
        }

        public static int Cosinus(int amplitude, int angle)
        {
            return (int)Math.Round(amplitude * Math.Cos(angle * Math.PI / 180d));
        }

        public static int Sinus(int amplitude, int angle)
        {
            return (int)Math.Round(amplitude * Math.Sin(angle * Math.PI / 180d));
        }

        #endregion


        #region sugar handling

        /// <summary>
        /// sugar respawn delay counter
        /// </summary>
        private int sugarDelay;

        /// <summary>
        /// Countdown number of total allowed sugar hills
        /// </summary>
        private int sugarCountDown;

        /// <summary>
        /// number of simultaneous existing sugar hills. 
        /// </summary>
        private int sugarLimit;

        /// <summary>
        /// Removes all empty sugar hills from list.
        /// </summary>
        private void removeSugar()
        {
            // TODO: speedup
            //List<CoreSugar> gemerkterZucker = new List<CoreSugar>();
            for (int i = 0; i < Playground.SugarHillsList.Count; i++)
            {
                CoreSugar sugar = Playground.SugarHillsList[i];
                if (sugar != null)
                {
                    if (sugar.Amount == 0)
                    {
                        //gemerkterZucker.Add(zucker);
                        //L�schen
                        Playground.RemoveSugar(sugar);
                        i--;
                    }
                }
            }
            //for(int i = 0; i < gemerkterZucker.Count; i++) {
            //  CoreSugar zucker = gemerkterZucker[i];
            //  if(zucker != null) {
            //    Playground.SugarHills.Remove(zucker);
            //  }
            //}
            //gemerkterZucker.Clear();
        }

        /// <summary>
        /// Spawns new sugar, if its time.
        /// </summary>
        private void spawnSugar()
        {
            if (Playground.SugarHillsList.Count < sugarLimit &&
               sugarDelay <= 0 &&
               sugarCountDown > 0)
            {
                sugarDelay = SimulationSettings.Custom.SugarRespawnDelay;
                sugarCountDown--;
                Playground.NewSugar();
            }
            sugarDelay--;
        }

        #endregion


        #region fruit-handling

        /// <summary>
        /// Delay-counter for fruit-respawn.
        /// </summary>
        private int fruitDelay;

        /// <summary>
        /// Counts down the total number of allowed fruits.
        /// </summary>
        private int fruitCountDown;

        /// <summary>
        /// Gets the count of simultaneous existing fruits. 
        /// </summary>
        private int fruitLimit;

        /// <summary>
        /// Spawns new fruit, if its time.
        /// </summary>
        private void spawnFruit()
        {
            if (Playground.FruitsList.Count < fruitLimit &&
               fruitDelay <= 0 &&
               fruitCountDown > 0)
            {
                fruitDelay = SimulationSettings.Custom.FruitRespawnDelay;
                fruitCountDown--;
                Playground.NewFruit();
            }
            fruitDelay--;
        }

        /// <summary>
        /// Removes fruit from list.
        /// </summary>
        /// <param name="colony">winning colony</param>
        private void removeFruit(CoreColony colony)
        {
            //List<CoreFruit> gemerktesObst = new List<CoreFruit>();
            for (int j = 0; j < Playground.FruitsList.Count; j++)
            {
                CoreFruit obst = Playground.FruitsList[j];
                for (int i = 0; i < colony.AntHills.Count; i++)
                {
                    CoreAnthill bau = colony.AntHills[i];
                    if (bau != null)
                    {
                        int entfernung = CoreCoordinate.DetermineDistanceI(obst.CoordinateBase, bau.CoordinateBase);
                        if (entfernung <= PLAYGROUND_UNIT)
                        {
                            //gemerktesObst.Add(obst);

                            // L�schen
                            colony.Statistic.CollectedFood += obst.Amount;
                            colony.Statistic.CollectedFruits++;
                            obst.Amount = 0;
                            for (int z = 0; z < obst.InsectsCarrying.Count; z++)
                            {
                                CoreInsect insect = obst.InsectsCarrying[z];
                                if (insect != null)
                                {
                                    insect.CarryingFruitBase = null;
                                    insect.CurrentLoadBase = 0;
                                    insect.DistanceToDestination = 0;
                                    insect.ResidualAngle = 0;
                                    insect.GoToAnthillBase();
                                }
                            }
                            obst.InsectsCarrying.Clear();
                            Playground.RemoveFruit(obst);
                            j--;
                        }
                    }
                }
            }
        }

        #endregion


        #region ant-handling

        /// <summary>
        /// Gets the count of simultaneous existing ants. 
        /// </summary>
        private int antLimit;

        /// <summary>
        /// Pr�ft ob eine Ameise an ihrem Ziel angekommen ist.
        /// </summary>
        /// <param name="ant">betroffene Ameise</param>
        private static void antAndTarget(CoreAnt ant)
        {
            // Ameisenbau.
            if (ant.TargetBase is CoreAnthill)
            {
                if (ant.CarryingFruitBase == null)
                {
                    ant.travelledDistance = 0;
                    ant.TargetBase = null;
                    ant.SmelledMarker.Clear();
                    ant.colony.Statistic.CollectedFood += ant.CurrentLoadBase;
                    ant.CurrentLoadBase = 0;
                    ant.currentEnergyBase = ant.MaximumEnergyBase;
                    ant.IsTiredBase = false;
                }
            }

            // Zuckerhaufen.
            else if (ant.TargetBase is CoreSugar)
            {
                CoreSugar zucker = (CoreSugar)ant.TargetBase;
                ant.TargetBase = null;
                if (zucker.Amount > 0)
                {
                    PlayerCall.TargetReached(ant, zucker);
                }
            }

            // Obstst�ck.
            else if (ant.TargetBase is CoreFruit)
            {
                CoreFruit obst = (CoreFruit)ant.TargetBase;
                ant.TargetBase = null;
                if (obst.Amount > 0)
                {
                    PlayerCall.TargetReached(ant, obst);
                }
            }

            // Insekt.
            else if (ant.TargetBase is CoreInsect) { }

            // Anderes Ziel.
            else
            {
                ant.TargetBase = null;
            }
        }

        /// <summary>
        /// Pr�ft ob eine Ameise einen Zuckerhaufen sieht.
        /// </summary>
        /// <param name="ant">betroffene Ameise</param>
        private void antAndSugar(CoreAnt ant)
        {
            for (int i = 0; i < Playground.SugarHillsList.Count; i++)
            {
                CoreSugar sugar = Playground.SugarHillsList[i];
                int entfernung = CoreCoordinate.DetermineDistanceI(ant.CoordinateBase, sugar.CoordinateBase);
                if (ant.TargetBase != sugar && entfernung <= ant.ViewRangeI)
                {
                    PlayerCall.Spots(ant, sugar);
                }
            }
        }

        /// <summary>
        /// Pr�ft ob eine Ameise ein Obsst�ck sieht.
        /// </summary>
        /// <param name="ameise">betroffene Ameise</param>
        private void antAndFruit(CoreAnt ameise)
        {
            for (int i = 0; i < Playground.FruitsList.Count; i++)
            {
                CoreFruit obst = Playground.FruitsList[i];
                int entfernung = CoreCoordinate.DetermineDistanceI(ameise.CoordinateBase, obst.CoordinateBase);
                if (ameise.TargetBase != obst && entfernung <= ameise.ViewRangeI)
                {
                    PlayerCall.Spots(ameise, obst);
                }
            }
        }

        /// <summary>
        /// Pr�ft ob die Ameise eine Markierung bemerkt.
        /// </summary>
        /// <param name="ameise">betroffene Ameise</param>
        private static void antAndMarkers(CoreAnt ameise)
        {
            CoreMarker marker = ameise.colony.Marker.FindMarker(ameise);
            if (marker != null)
            {
                PlayerCall.SmellsFriend(ameise, marker);
                ameise.SmelledMarker.Add(marker);
            }
        }

        /// <summary>
        /// Erntfernt Ameisen die keine Energie mehr haben.
        /// </summary>
        /// <param name="colony">betroffenes Volk</param>
        private void removeAnt(CoreColony colony)
        {
            List<CoreAnt> liste = new List<CoreAnt>();

            for (int i = 0; i < colony.StarvedInsects.Count; i++)
            {
                CoreAnt ant = colony.StarvedInsects[i] as CoreAnt;
                if (ant != null && !liste.Contains(ant))
                {
                    liste.Add(ant);
                    colony.Statistic.StarvedAnts++;
                    PlayerCall.HasDied(ant, CoreKindOfDeath.Starved);
                }
            }

            for (int i = 0; i < colony.EatenInsects.Count; i++)
            {
                CoreAnt ant = colony.EatenInsects[i] as CoreAnt;
                if (ant != null && !liste.Contains(ant))
                {
                    liste.Add(ant);
                    colony.Statistic.EatenAnts++;
                    PlayerCall.HasDied(ant, CoreKindOfDeath.Eaten);
                }
            }

            for (int i = 0; i < colony.BeatenInsects.Count; i++)
            {
                CoreAnt ant = colony.BeatenInsects[i] as CoreAnt;
                if (ant != null)
                {
                    if (!liste.Contains(ant))
                    {
                        liste.Add(ant);
                        colony.Statistic.BeatenAnts++;
                        PlayerCall.HasDied(ant, CoreKindOfDeath.Beaten);
                    }
                }
            }

            for (int i = 0; i < liste.Count; i++)
            {
                CoreAnt ant = liste[i];
                if (ant != null)
                {
                    colony.RemoveInsect(ant);

                    for (int j = 0; j < Playground.FruitsList.Count; j++)
                    {
                        CoreFruit fruit = Playground.FruitsList[j];
                        fruit.InsectsCarrying.Remove(ant);
                    }
                }
            }

            colony.StarvedInsects.Clear();
            colony.EatenInsects.Clear();
            colony.BeatenInsects.Clear();
        }

        /// <summary>
        /// Erzeugt neue Ameisen.
        /// </summary>
        /// <param name="colony">betroffenes Volk</param>
        private void spawnAnt(CoreColony colony)
        {
            if (colony.InsectsList.Count < antLimit &&
               colony.insectDelay < 0 &&
               colony.insectCountDown > 0)
            {
                colony.NewInsect(random);
                colony.insectDelay = SimulationSettings.Custom.AntRespawnDelay;
                colony.insectCountDown--;
            }
            colony.insectDelay--;
        }

        // Bewegt Obsst�cke und alle Insekten die das Obsst�ck tragen.
        private void MoveFruitsAndInsects()
        {
            Playground.FruitsList.ForEach(delegate (CoreFruit fruit)
            {
                if (fruit.InsectsCarrying.Count > 0)
                {
                    int dx = 0;
                    int dy = 0;
                    int last = 0;

                    fruit.InsectsCarrying.ForEach(delegate (CoreInsect insect)
                    {
                        if (insect.TargetBase != fruit && insect.ResidualAngle == 0)
                        {
                            dx += Cos[insect.currentSpeedI, insect.DirectionBase];
                            dy += Sin[insect.currentSpeedI, insect.DirectionBase];
                            last += insect.CurrentLoadBase;
                        }
                    });

                    last = Math.Min((int)(last * SimulationSettings.Custom.FruitLoadMultiplier), fruit.Amount);
                    dx = dx * last / fruit.Amount / fruit.InsectsCarrying.Count;
                    dy = dy * last / fruit.Amount / fruit.InsectsCarrying.Count;

                    fruit.CoordinateBase = new CoreCoordinate(fruit.CoordinateBase, dx, dy);
                    fruit.InsectsCarrying.ForEach(
                      delegate (CoreInsect insect) { insect.CoordinateBase = new CoreCoordinate(insect.CoordinateBase, dx, dy); });
                }
            });
            //foreach(CoreFruit obst in Playground.Fruits) {
            //  if(obst.TragendeInsekten.Count > 0) {
            //    int dx = 0;
            //    int dy = 0;
            //    int last = 0;

            //    foreach(CoreInsect insekt in obst.TragendeInsekten) {
            //      if(insekt.ZielBase != obst && insekt.RestWinkelBase == 0) {
            //        dx += Cos[insekt.aktuelleGeschwindigkeitI, insekt.RichtungBase];
            //        dy += Sin[insekt.aktuelleGeschwindigkeitI, insekt.RichtungBase];
            //        last += insekt.AktuelleLastBase;
            //      }
            //    }

            //    last = Math.Min((int)(last * SimulationSettings.Settings.FruitLoadMultiplier), obst.Menge);
            //    dx = dx * last / obst.Menge / obst.TragendeInsekten.Count;
            //    dy = dy * last / obst.Menge / obst.TragendeInsekten.Count;

            //    obst.Coordinate = new CoreCoordinate(obst.Coordinate, dx, dy);

            //    foreach(CoreInsect insekt in obst.TragendeInsekten) {
            //      insekt.Coordinate = new CoreCoordinate(insekt.Coordinate, dx, dy);
            //    }
            //  }
            //}
        }

        #endregion


        #region marker-handling

        /// <summary>
        /// Entfernt abgelaufene Markierungen und erzeugt neue Markierungen.
        /// </summary>
        /// <param name="colony">betroffenes Volk</param>
        private static void updateMarkers(CoreColony colony)
        {
            // TODO: Settings ber�cksichtigen
            // Markierungen aktualisieren und inaktive Markierungen l�schen.
            List<CoreMarker> gemerkteMarkierungen = new List<CoreMarker>();

            foreach (CoreMarker markierung in colony.Marker)
            {
                if (markierung.IsActive)
                {
                    markierung.Update();
                }
                else
                {
                    gemerkteMarkierungen.Add(markierung);
                }
            }
            gemerkteMarkierungen.ForEach(delegate (CoreMarker marker)
            {
                colony.InsectsList.ForEach(delegate (CoreInsect insect)
                {
                    CoreAnt ant = insect as CoreAnt;
                    if (ant != null)
                    {
                        ant.SmelledMarker.Remove(marker);
                    }
                });
            });
            colony.Marker.Remove(gemerkteMarkierungen);

            // Neue Markierungen �berpr�fen und hinzuf�gen.
            gemerkteMarkierungen.Clear();
            colony.NewMarker.ForEach(delegate (CoreMarker newMarker)
            {
                bool zuNah = false;
                foreach (CoreMarker markierung in colony.Marker)
                {
                    int entfernung =
                      CoreCoordinate.DetermineDistanceToCenter
                        (markierung.CoordinateBase, newMarker.CoordinateBase);
                    if (entfernung < SimulationSettings.Custom.MarkerDistance * PLAYGROUND_UNIT)
                    {
                        zuNah = true;
                        break;
                    }
                }
                if (!zuNah)
                {
                    colony.Marker.Add(newMarker);
                }
            });
            colony.NewMarker.Clear();
        }

        #endregion


        #region bug-handling

        /// <summary>
        /// Gets the count of simultaneous existing bugs. 
        /// </summary>
        private int bugLimit;

        /// <summary>
        /// Remove dead bugs.
        /// </summary>
        private void removeBugs()
        {
            for (int i = 0; i < Bugs.EatenInsects.Count; i++)
            {
                CoreBug bug = Bugs.EatenInsects[i] as CoreBug;
                if (bug != null)
                {
                    Bugs.InsectsList.Remove(bug);
                }
            }
            Bugs.EatenInsects.Clear();
        }

        /// <summary>
        /// Heals the bugs, if its time.
        /// </summary>
        private void healBugs()
        {
            if (currentRound % SimulationSettings.Custom.BugRegenerationDelay == 0)
            {
                for (int i = 0; i < Bugs.InsectsList.Count; i++)
                {
                    CoreBug bug = Bugs.InsectsList[i] as CoreBug;
                    if (bug != null)
                    {
                        if (bug.currentEnergyBase < bug.MaximumEnergyBase)
                        {
                            bug.currentEnergyBase += SimulationSettings.Custom.BugRegenerationValue;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Spawn new bugs, if needed.
        /// </summary>
        private void spawnBug()
        {
            if (Bugs.InsectsList.Count < bugLimit &&
               Bugs.insectDelay < 0 &&
               Bugs.insectCountDown > 0)
            {
                Bugs.NewInsect(random);
                Bugs.insectDelay = SimulationSettings.Custom.BugRespawnDelay;
                Bugs.insectCountDown--;
            }
            Bugs.insectDelay--;
        }

        #endregion
    }
}