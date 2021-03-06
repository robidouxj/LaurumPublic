﻿using Mirror;
using System.Collections;
using UnityEngine;


public class EnemyAttack : MonoBehaviour
{

    #region Enemy
    EnemyStats EnemyStats;
    EnemyAggro EnemyAggro;
    EnemyTakeDamage EnemyTakeDamage;
    EnemyConditions EnemyConditions;
    EnemySpawnInfo EnemySpawnInfo;
    Pathfinding.EnemyControllerAI EnemyControllerAI;
    #endregion

    #region Enemy Behaviour
    public enum EnemyBehaviours//one behaviour can have different skills
    {
        normal,
        fire_boss_1,
        axe_boss_1
    }
    public EnemyBehaviours EnemyBehaviour;
    #region temp data
    Coroutine temp_control_ricochet_projectile;
    #endregion
    #endregion

    #region temp data
    [HideInInspector]
    public GameObject PlayerToAttack;
    bool isAttacking = false;
    bool can_attack = true;
    #endregion

    private void Awake()
    {
        EnemySpawnInfo = GetComponent<EnemySpawnInfo>();
        EnemyTakeDamage = GetComponent<EnemyTakeDamage>();
        EnemyAggro = GetComponent<EnemyAggro>();
        EnemyStats = GetComponent<EnemyStats>();
        EnemyConditions = GetComponent<EnemyConditions>();
        EnemyControllerAI = GetComponent<Pathfinding.EnemyControllerAI>();
    }
    void Start()
    {
        StartCoroutine("_FixedUpdate");

    }

    IEnumerator _FixedUpdate()
    {
        if (EnemyConditions.silence)
        {
            if (EnemyStats.DamageType_now == EnemyStats.DamageType.magical && (EnemyStats.MonsterType_now == EnemyStats.MonsterType.normal || EnemyStats.MonsterType_now == EnemyStats.MonsterType.elite))
            {
                can_attack = false;
            }
        }
        else
        {
            can_attack = true;
        }
        if (PlayerToAttack != null && !EnemyConditions.stunned && can_attack)
        {

            if (Vector2.Distance(PlayerToAttack.transform.position, transform.position) <= EnemyStats.AttackRange)
            {
                if (isAttacking == false)
                {
                    if (!PlayerToAttack.GetComponent<NetworkProximityChecker>().forceHidden)
                    {
                        isAttacking = true;
                        AttackNow();
                        yield return new WaitForSeconds(EnemyStats.AttackSpeed);
                        isAttacking = false;
                    }
                    else
                    {
                        EnemyAggro.resetAggro(false);
                    }
                }
            }
            else
            {
                StopCoroutine("Attacking");
                isAttacking = false;
            }
        }
        yield return new WaitForSeconds(0.5f);
        StartCoroutine("_FixedUpdate");
    }

    #region Utilidades  

    #endregion

    #region Enemy auto attack
    public void AttackNow()
    {
        if (EnemySpawnInfo.x_ObjectHelper.Raycast_didItHit(gameObject, PlayerToAttack, EnemyStats.AttackRange, LayerMask.GetMask("Player", "decoy", "Coliders")))
        {
            if (PlayerToAttack.tag == "Player")
            {
                if (PlayerToAttack.GetComponent<PlayerStats>().CurrentHP > 0)
                {

                    bool Critico = false;
                    bool dodged = false;
                    float DamageTX = CalculateDamageTx(PlayerToAttack);

                    //calculamos el critico
                    if (Random.Range(0, 100) <= EnemyStats.Critical_percent_agi)
                    {
                        Critico = true;
                        DamageTX = Mathf.RoundToInt(DamageTX * 2);
                    }
                    float Adj_dodge_chance = 5; //JWR - Moving dodge bonus
                    float Player_dodge_chance = PlayerToAttack.GetComponent<PlayerStats>().Dodge_chance;
                    float Dodge_hard_cap = PlayerToAttack.GetComponent<PlayerStats>().Dodge_hard_cap;
	                if (Player_dodge_chance > (Dodge_hard_cap - 5)) { Adj_dodge_chance = Player_dodge_chance - (Dodge_hard_cap - 5); }
	                if (Player_dodge_chance >= Dodge_hard_cap) { Adj_dodge_chance = 0; }
                    if (!PlayerMPSync.stationary) // JWR
                    {
	    	            Adj_dodge_chance += PlayerStats.Dodge_chance; // JWR - Add bonus if moving
	                }
                    else
	                {
	    	            Adj_dodge_chance = PlayerStats.Dodge_chance; // JWR - No bonus if stationary
	                }
                    if (Random.Range(0, 100) <= Adj_dodge_chance)
                    {
                        dodged = true;
                        DamageTX = 0;
                    }

                    //MOD ReflectDMG
                    if (DamageTX > 0 && !dodged)
                    {

                        DamageTX = Random.Range(Mathf.RoundToInt(DamageTX * 0.95f), Mathf.RoundToInt(DamageTX * 1.05f));

                        //Linked hearts
                        var buff_info = PlayerToAttack.GetComponent<PlayerConditions>().get_buff_information(PlayerConditions.type.buff, 20);
                        if (buff_info != null)
                        {
                            if (buff_info.skill_owner != null && buff_info.skill_owner.GetComponent<PlayerStats>().CurrentHP > 0)
                            {
                                //damage to paladin
                                var linked_damage = Mathf.RoundToInt(DamageTX * buff_info.skill_requested.multipliers[1] / 100f);
                                buff_info.skill_owner.GetComponent<PlayerStats>().hpChange(-linked_damage);
                                buff_info.skill_owner.GetComponent<PlayerGeneral>().showCBT(buff_info.skill_owner, true, false, linked_damage, "damage");
                                //damage portion
                                DamageTX = DamageTX - linked_damage;
                            }
                            else
                            {
                                PlayerToAttack.GetComponent<PlayerConditions>().remove_buff_debuff(PlayerConditions.type.buff, 20);
                            }
                        }
                        //Burn on touch
                        buff_info = PlayerToAttack.GetComponent<PlayerConditions>().get_buff_information(PlayerConditions.type.buff, 21);
                        if (buff_info != null)
                        {
                            PlayerToAttack.GetComponent<PlayerConditions>().remove_buff_debuff(PlayerConditions.type.buff, 21);
                            EnemyConditions.handle_effect(DOT_effect.effect_type.fire, buff_info.skill_requested.multipliers[0], PlayerToAttack);
                        }

                        var reflectSTR = PlayerToAttack.GetComponent<PlayerStats>().modReflectSTR * DamageTX / 100;
                        if (reflectSTR > 0)
                        {
                            reflectSTR = Mathf.Ceil(reflectSTR);
                            EnemyStats.CurrentHP = EnemyStats.CurrentHP - reflectSTR;
                            PlayerToAttack.GetComponent<PlayerGeneral>().showCBT(gameObject, false, false, (int)reflectSTR, "reflect");
                            if (EnemyStats.CurrentHP <= 0)
                            {
                                EnemyTakeDamage.dieNow();
                                return;
                            }


                        }

                        DamageTX = EnemySpawnInfo.x_ObjectHelper.DamagePlayerNow(PlayerToAttack, DamageTX, EnemyStats.MobName, gameObject);
                        
                    }
                    EnemyStats.RpcMakeSound("auto_hit_tx", transform.position);
                   
                    PlayerToAttack.GetComponent<PlayerGeneral>().showCBT(PlayerToAttack, Critico, dodged, (int)DamageTX, "damage");
                    PlayerToAttack.GetComponent<PlayerGeneral>().send_autoATK_animation(gameObject, PlayerToAttack);
                }
                else
                {
                    EnemyAggro.resetAggro(true);
                }
            }
            else if (PlayerToAttack.tag == "decoy")
            {
                PlayerToAttack.GetComponent<DecoyGeneral>().hit_decoy();
            }
        }


    }
    public int CalculateDamageTx(GameObject toPlayer)
    {

        float DamageTx = 0;
        float playerTotalDef = 0;
        float monsterFinalDamage = 0;


        if (EnemyStats.DamageType_now == EnemyStats.DamageType.physical) // Physical
        {
            playerTotalDef = toPlayer.GetComponent<PlayerStats>().Defense_str + (toPlayer.GetComponent<PlayerStats>().Defense_int * 0.2f);
            monsterFinalDamage = EnemyStats.Damage_str;
        }
        else // Magical
        {
            playerTotalDef = toPlayer.GetComponent<PlayerStats>().Defense_int + (toPlayer.GetComponent<PlayerStats>().Defense_str * 0.2f);
            monsterFinalDamage = EnemyStats.Damage_int;
        }
        //.LogError("playerTotalDef=" + playerTotalDef + " monsterFinalDamage="+ monsterFinalDamage);
        DamageTx = EnemySpawnInfo.x_ObjectHelper.CalculateEnemyToPlayerDamage(playerTotalDef, monsterFinalDamage);
        //.LogError("Damage Player <-- Enemy: EnemyDamage=" + DamageTx);

        if (EnemySpawnInfo.isInDevilSquare)
        {
            var value = EnemyStats.Level / PlayerToAttack.GetComponent<PlayerStats>().PlayerLevel;

            if (value > 1f)
            {
                DamageTx *= value;
            }

        }
        else
        {
            int levelDiff = EnemyStats.Level - PlayerToAttack.GetComponent<PlayerStats>().PlayerLevel;
            if (levelDiff > 5)
            {
                float damageIncrease = (100f + (levelDiff * 2f)) / 100f;
                if (damageIncrease < 0.05f)
                    damageIncrease = 0.05f;

                DamageTx = Mathf.RoundToInt(DamageTx * damageIncrease);

            }

        }
        if (DamageTx < 0)
        {
            DamageTx = 0;
        }

        return (int)DamageTx;
    }
    #endregion

    #region Behaviour control
    bool[] already_happened;
    bool triggered;
    public void reset_behavioursFlags()
    {
        already_happened = new bool[4];
    }
    public void do_what_you_have_to()
    {
        if (!triggered)
        {
            switch (EnemyBehaviour)
            {

                case EnemyBehaviours.fire_boss_1:
                    //while hp is >30% there is 50% chance to trigger fire ricochet every 5s
                    if (!already_happened[0])
                    {
                        if (EnemyStats.CurrentHP / EnemyStats.MaxHP > 0.3f)
                        {
                            if (Random.Range(1, 100) <= 50)
                            {
                                already_happened[0] = true;
                                EnemySpawnInfo.x_ObjectHelper.GeneralSkills.ricochet_projectile(false, gameObject, PlayerToAttack, DOT_effect.effect_type.fire, EnemyStats.Damage_int, EnemyStats.Defense_str);
                                EnemyAggro.Rpc_show_CBT("", true, "Fireball!");
                                EnemyStats.RpcMakeSound("boss_fireball", transform.position);
                                StartCoroutine(reset_action(0, 5f));
                            }
                        }
                    }
                    //reflect damage for 15 seconds when hp reaches 60% - cant walk for 5 seconds
                    if (!already_happened[1])
                    {
                        if (EnemyStats.CurrentHP / EnemyStats.MaxHP <= 0.5f)
                        {
                            if (!EnemyConditions.buffs.Contains(12))
                            {
                                already_happened[1] = true;
                                //buff
                                EnemyConditions.buffs.Add(12);
                                EnemyConditions.reflect = true;
                                EnemyConditions.tracker_list.Add(new EnemyConditions.track_buff_debuffs(12, null, Time.time + 15f, null, false, EnemyConditions.type.buff, false));
                                EnemyAggro.Rpc_show_CBT("", true, "Reflecting!");
                                EnemyStats.RpcMakeSound("boss_reflect", transform.position);
                                //debuff                                
                                EnemyConditions.de_buffs.Add(1);
                                EnemyConditions.stunned = true;
                                EnemyControllerAI.canMove = false;
                                EnemyConditions.tracker_list.Add(new EnemyConditions.track_buff_debuffs(1, null, Time.time + 5f, null, false, EnemyConditions.type.debuff, false));

                            }
                        }
                    }
                    //heal from absorbed damage for 5seconds when hp reaches 20% (dont move)
                    if (!already_happened[2])
                    {
                        if (EnemyStats.CurrentHP / EnemyStats.MaxHP <= 0.2f)
                        {
                            if (!EnemyConditions.buffs.Contains(13))
                            {
                                already_happened[2] = true;
                                //buff
                                EnemyConditions.buffs.Add(13);
                                EnemyConditions.converting_dmg_to_hp = true;
                                EnemyConditions.tracker_list.Add(new EnemyConditions.track_buff_debuffs(13, null, Time.time + 15f, null, false, EnemyConditions.type.buff, false));
                                EnemyAggro.Rpc_show_CBT("", true, "Absorbing HP...");
                                EnemyStats.RpcMakeSound("boss_absorb", transform.position);
                                //debuff                                
                                EnemyConditions.de_buffs.Add(1);
                                EnemyConditions.stunned = true;
                                EnemyControllerAI.canMove = false;
                                EnemyConditions.tracker_list.Add(new EnemyConditions.track_buff_debuffs(1, null, Time.time + 5f, null, false, EnemyConditions.type.debuff, false));
                            }
                        }
                    }
                    //super speed and critical when hp reaches 10%
                    if (!already_happened[3])
                    {
                        if (EnemyStats.CurrentHP / EnemyStats.MaxHP <= 0.15f)
                        {

                            already_happened[3] = true;
                            EnemyConditions.buffs.Add(1);
                            EnemyConditions.tracker_list.Add(new EnemyConditions.track_buff_debuffs(1, null, Time.time + 15f, null, false, EnemyConditions.type.buff, false));
                            EnemyConditions.buffs.Add(4);
                            EnemyConditions.tracker_list.Add(new EnemyConditions.track_buff_debuffs(4, null, Time.time + 15f, null, false, EnemyConditions.type.buff, false));
                            EnemyAggro.Rpc_show_CBT("", true, "Enraged!");
                            EnemyStats.RpcMakeSound("boss_enraged", transform.position);
                        }
                    }
                    StartCoroutine(behaviour_watchdog());
                    triggered = true;
                    break;
                case EnemyBehaviours.axe_boss_1:
                    //while hp is >30% there is 50% chance to trow 4 to 8 axes in different directions
                    if (!already_happened[0])
                    {
                        if (EnemyStats.CurrentHP / EnemyStats.MaxHP > 0.3f)
                        {
                            if (Random.Range(1, 100) <= 50)
                            {
                                already_happened[0] = true;
                                EnemySpawnInfo.x_ObjectHelper.GeneralSkills.multiple_projectiles(1.1f, transform.position, Random.Range(4, 8), DOT_effect.effect_type.bleed, Mathf.RoundToInt(EnemyStats.Damage_int * 0.8f), EnemyStats.Defense_str, false, 4f);
                                EnemyAggro.Rpc_show_CBT("", true, "Multi Axe");
                                EnemyStats.RpcMakeSound("boss_multi_axe", transform.position);
                                StartCoroutine(reset_action(0, 6f));
                            }
                        }
                    }
                    //starting on 80% it applies confusion to everyone around every 10s
                    if (!already_happened[1])
                    {
                        if (EnemyStats.CurrentHP / EnemyStats.MaxHP <= 0.8f)
                        {
                            already_happened[1] = true;
                            var players_around = EnemySpawnInfo.x_ObjectHelper.get_AOE_LOS_targets(gameObject, 1f, LayerMask.GetMask("Player"), false);
                            for (int i = 0; i < players_around.Count; i++)
                            {
                                if (players_around[i].GetComponent<PlayerStats>().CurrentHP > 0)
                                {
                                    //confuse player
                                    players_around[i].GetComponent<PlayerConditions>().de_buffs.Add(5);
                                    players_around[i].GetComponent<PlayerConditions>().tracker_list.Add(new PlayerConditions.track_buff_debuffs(5, null, Time.time + 10f, null, false, PlayerConditions.type.debuff, true));

                                }

                            }
                            EnemyAggro.Rpc_show_CBT("", true, "Mass Confusion");
                            EnemyStats.RpcMakeSound("boss_mass_conf", transform.position);
                            StartCoroutine(reset_action(1, 10f));
                        }
                    }
                    //on 40% weak+slow to everyone around and buffs itself with speed and crit
                    if (!already_happened[2])
                    {
                        if (EnemyStats.CurrentHP / EnemyStats.MaxHP <= 0.4f)
                        {
                            already_happened[2] = true;
                            EnemyConditions.buffs.Add(1);
                            EnemyConditions.tracker_list.Add(new EnemyConditions.track_buff_debuffs(1, null, Time.time + 15f, null, false, EnemyConditions.type.buff, false));
                            EnemyConditions.buffs.Add(4);
                            EnemyConditions.tracker_list.Add(new EnemyConditions.track_buff_debuffs(4, null, Time.time + 15f, null, false, EnemyConditions.type.buff, false));
                            EnemyAggro.Rpc_show_CBT("", true, "Enraged!");
                            EnemyStats.RpcMakeSound("boss_enraged", transform.position);
                            var players_around = EnemySpawnInfo.x_ObjectHelper.get_AOE_LOS_targets(gameObject, 1f, LayerMask.GetMask("Player"), false);
                            for (int i = 0; i < players_around.Count; i++)
                            {
                                if (players_around[i].GetComponent<PlayerStats>().CurrentHP > 0)
                                {
                                    //weak
                                    players_around[i].GetComponent<PlayerConditions>().de_buffs.Add(8);
                                    players_around[i].GetComponent<PlayerConditions>().decreasedDamage = -50f;
                                    players_around[i].GetComponent<PlayerConditions>().tracker_list.Add(new PlayerConditions.track_buff_debuffs(8, null, Time.time + 5f, null, false, PlayerConditions.type.debuff, true));

                                    //slow
                                    players_around[i].GetComponent<PlayerConditions>().de_buffs.Add(2);
                                    players_around[i].GetComponent<PlayerConditions>().tracker_list.Add(new PlayerConditions.track_buff_debuffs(2, null, Time.time + 15f, null, false, PlayerConditions.type.debuff, true));

                                    //update stats
                                    players_around[i].GetComponent<PlayerStats>().ProcessStats();
                                }

                            }

                        }
                    }
                    StartCoroutine(behaviour_watchdog());
                    triggered = true;
                    break;

                default:
                    break;
            }

        }

    }
    IEnumerator reset_action(int action, float time)
    {
        yield return new WaitForSeconds(time);
        already_happened[action] = false;
    }
    IEnumerator behaviour_watchdog()
    {
        yield return new WaitForSeconds(2f);
        triggered = false;
        if (EnemyAggro.isAggroed)
        {
            do_what_you_have_to();
        }

    }
    #endregion


}
