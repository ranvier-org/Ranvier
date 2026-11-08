
type __ = Obj.t

type unit0 =
| Tt

type bool =
| True
| False

val negb : bool -> bool

type nat =
| O
| S of nat

type 'a option =
| Some of 'a
| None

val option_map : ('a1 -> 'a2) -> 'a1 option -> 'a2 option

type ('a, 'b) prod =
| Pair of 'a * 'b

val fst : ('a1, 'a2) prod -> 'a1

val snd : ('a1, 'a2) prod -> 'a2

type 'a list =
| Nil
| Cons of 'a * 'a list

val length : 'a1 list -> nat

val app : 'a1 list -> 'a1 list -> 'a1 list

type comparison =
| Eq
| Lt
| Gt

val compOpp : comparison -> comparison

val id : __ -> __

type sumbool =
| Left
| Right

val pred : nat -> nat

val add : nat -> nat -> nat

val sub : nat -> nat -> nat

val compose : ('a2 -> 'a3) -> ('a1 -> 'a2) -> 'a1 -> 'a3

module Nat :
 sig
  val eqb : nat -> nat -> bool

  val max : nat -> nat -> nat

  val eq_dec : nat -> nat -> sumbool
 end

val seq : nat -> nat -> nat list

val firstn : nat -> 'a1 list -> 'a1 list

val skipn : nat -> 'a1 list -> 'a1 list

val tl : 'a1 list -> 'a1 list

val rev : 'a1 list -> 'a1 list

val fold_left : ('a1 -> 'a2 -> 'a1) -> 'a2 list -> 'a1 -> 'a1

val fold_right : ('a2 -> 'a1 -> 'a1) -> 'a1 -> 'a2 list -> 'a1

val le_lt_dec : nat -> nat -> sumbool

val le_gt_dec : nat -> nat -> sumbool

val le_dec : nat -> nat -> sumbool

val lt_dec : nat -> nat -> sumbool

type decision = sumbool

val decide : decision -> sumbool

type ('a, 'b) relDecision = 'a -> 'b -> decision

val decide_rel : ('a1, 'a2) relDecision -> 'a1 -> 'a2 -> decision

type 'a empty = 'a

val empty0 : 'a1 empty -> 'a1

type 'a union = 'a -> 'a -> 'a

val union0 : 'a1 union -> 'a1 -> 'a1 -> 'a1

type 'a difference = 'a -> 'a -> 'a

val difference0 : 'a1 difference -> 'a1 -> 'a1 -> 'a1

type ('a, 'b) singleton = 'a -> 'b

val singleton0 : ('a1, 'a2) singleton -> 'a1 -> 'a2

val list_to_set :
  ('a1, 'a2) singleton -> 'a2 empty -> 'a2 union -> 'a1 list -> 'a2

type ('a, 'b) filter = __ -> ('a -> decision) -> 'b -> 'b

val filter0 : ('a1, 'a2) filter -> ('a1 -> decision) -> 'a2 -> 'a2

type 'm mBind = __ -> __ -> (__ -> 'm) -> 'm -> 'm

val mbind : 'a1 mBind -> ('a2 -> 'a1) -> 'a1 -> 'a1

type 'm fMap = __ -> __ -> (__ -> __) -> 'm -> 'm

val fmap : 'a1 fMap -> ('a2 -> 'a3) -> 'a1 -> 'a1

type 'm oMap = __ -> __ -> (__ -> __ option) -> 'm -> 'm

val omap : 'a1 oMap -> ('a2 -> 'a3 option) -> 'a1 -> 'a1

type ('k, 'a, 'm) lookup = 'k -> 'm -> 'a option

val lookup0 : ('a1, 'a2, 'a3) lookup -> 'a1 -> 'a3 -> 'a2 option

type ('k, 'a, 'm) singletonM = 'k -> 'a -> 'm

val singletonM0 : ('a1, 'a2, 'a3) singletonM -> 'a1 -> 'a2 -> 'a3

type ('k, 'a, 'm) insert = 'k -> 'a -> 'm -> 'm

val insert0 : ('a1, 'a2, 'a3) insert -> 'a1 -> 'a2 -> 'a3 -> 'a3

type ('k, 'a, 'm) alter = ('a -> 'a) -> 'k -> 'm -> 'm

val alter0 : ('a1, 'a2, 'a3) alter -> ('a2 -> 'a2) -> 'a1 -> 'a3 -> 'a3

type ('k, 'a, 'm) partialAlter = ('a option -> 'a option) -> 'k -> 'm -> 'm

val partial_alter :
  ('a1, 'a2, 'a3) partialAlter -> ('a2 option -> 'a2 option) -> 'a1 -> 'a3 ->
  'a3

type ('m, 'd) dom = 'm -> 'd

val dom0 : ('a1, 'a2) dom -> 'a1 -> 'a2

type 'm merge =
  __ -> __ -> __ -> (__ option -> __ option -> __ option) -> 'm -> 'm -> 'm

val merge0 :
  'a1 merge -> ('a2 option -> 'a3 option -> 'a4 option) -> 'a1 -> 'a1 -> 'a1

type ('a, 'm) unionWith = ('a -> 'a -> 'a option) -> 'm -> 'm -> 'm

val union_with :
  ('a1, 'a2) unionWith -> ('a1 -> 'a1 -> 'a1 option) -> 'a2 -> 'a2 -> 'a2

type ('a, 'm) differenceWith = ('a -> 'a -> 'a option) -> 'm -> 'm -> 'm

val difference_with :
  ('a1, 'a2) differenceWith -> ('a1 -> 'a1 -> 'a1 option) -> 'a2 -> 'a2 -> 'a2

type ('a, 'c) elements = 'c -> 'a list

val elements0 : ('a1, 'a2) elements -> 'a2 -> 'a1 list

type ('a, 'c) fresh = 'c -> 'a

val fresh0 : ('a1, 'a2) fresh -> 'a2 -> 'a1

type 'a infinite =
  ('a, 'a list) fresh
  (* singleton inductive, whose constructor was Build_Infinite *)

type positive =
| XI of positive
| XO of positive
| XH

type n =
| N0
| Npos of positive

type z =
| Z0
| Zpos of positive
| Zneg of positive

module Pos :
 sig
  val succ : positive -> positive

  val add : positive -> positive -> positive

  val add_carry : positive -> positive -> positive

  val pred_double : positive -> positive

  type mask =
  | IsNul
  | IsPos of positive
  | IsNeg

  val succ_double_mask : mask -> mask

  val double_mask : mask -> mask

  val double_pred_mask : positive -> mask

  val sub_mask : positive -> positive -> mask

  val sub_mask_carry : positive -> positive -> mask

  val mul : positive -> positive -> positive

  val compare_cont : comparison -> positive -> positive -> comparison

  val compare : positive -> positive -> comparison

  val eqb : positive -> positive -> bool

  val iter_op : ('a1 -> 'a1 -> 'a1) -> positive -> 'a1 -> 'a1

  val to_nat : positive -> nat

  val of_succ_nat : nat -> positive
 end

module Coq_Pos :
 sig
  val succ : positive -> positive

  val pred_double : positive -> positive

  val pred : positive -> positive

  val eq_dec : positive -> positive -> sumbool
 end

module N :
 sig
  val succ_double : n -> n

  val double : n -> n

  val sub : n -> n -> n

  val compare : n -> n -> comparison

  val leb : n -> n -> bool

  val pos_div_eucl : positive -> n -> (n, n) prod
 end

module Coq_N :
 sig
  val to_nat : n -> nat

  val of_nat : nat -> n

  val eq_dec : n -> n -> sumbool
 end

module Z :
 sig
  val double : z -> z

  val succ_double : z -> z

  val pred_double : z -> z

  val pos_sub : positive -> positive -> z

  val add : z -> z -> z

  val opp : z -> z

  val sub : z -> z -> z

  val mul : z -> z -> z

  val compare : z -> z -> comparison

  val ltb : z -> z -> bool

  val eqb : z -> z -> bool

  val to_nat : z -> nat

  val of_nat : nat -> z

  val of_N : n -> z

  val quotrem : z -> z -> (z, z) prod

  val quot : z -> z -> z

  val eq_dec : z -> z -> sumbool
 end

val not_dec : decision -> decision

val and_dec : decision -> decision -> decision

val or_dec : decision -> decision -> decision

val prod_eq_dec :
  ('a1, 'a1) relDecision -> ('a2, 'a2) relDecision -> (('a1, 'a2) prod, ('a1,
  'a2) prod) relDecision

val bool_decide : decision -> bool

val from_option : ('a1 -> 'a2) -> 'a2 -> 'a1 option -> 'a2

val option_eq_dec :
  ('a1, 'a1) relDecision -> ('a1 option, 'a1 option) relDecision

val option_bind : (__ -> __ option) -> __ option -> __ option

val option_fmap : (__ -> __) -> __ option -> __ option

val option_union_with : ('a1, 'a1 option) unionWith

val option_difference_with : ('a1, 'a1 option) differenceWith

val z_le_dec : z -> z -> sumbool

module Coq_Nat :
 sig
  val eq_dec : (nat, nat) relDecision

  val le_dec : (nat, nat) relDecision

  val lt_dec : (nat, nat) relDecision
 end

module Coq0_Pos :
 sig
  val eq_dec : (positive, positive) relDecision

  val reverse_go : positive -> positive -> positive

  val reverse : positive -> positive
 end

module Coq0_N :
 sig
  val eq_dec : (n, n) relDecision
 end

module Coq_Z :
 sig
  val eq_dec : (z, z) relDecision

  val le_dec : (z, z) relDecision
 end

module Coq_list :
 sig
  val list_lookup : (nat, 'a1, 'a1 list) lookup

  val list_alter : (nat, 'a1, 'a1 list) alter

  val list_insert : (nat, 'a1, 'a1 list) insert

  val list_filter : ('a1 -> decision) -> 'a1 list -> 'a1 list

  val list_elem_of_dec : ('a1, 'a1) relDecision -> ('a1, 'a1 list) relDecision
 end

module Coq0_list :
 sig
  val list_fmap : (__ -> __) -> __ list -> __ list

  val list_omap : (__ -> __ option) -> __ list -> __ list
 end

type 'a countable = { encode : ('a -> positive);
                      decode : (positive -> 'a option) }

val prod_encode_fst : positive -> positive

val prod_encode_snd : positive -> positive

val prod_encode : positive -> positive -> positive

val prod_decode_fst : positive -> positive option

val prod_decode_snd : positive -> positive option

val prod_countable :
  ('a1, 'a1) relDecision -> 'a1 countable -> ('a2, 'a2) relDecision -> 'a2
  countable -> ('a1, 'a2) prod countable

val n_countable : n countable

val nat_countable : nat countable

val set_fresh :
  ('a1, 'a2) elements -> ('a1, 'a1 list) fresh -> ('a1, 'a2) fresh

val max_infinite : ('a1 -> 'a1 -> 'a1) -> 'a1 -> 'a1 infinite

val nat_infinite : nat infinite

type ('k, 'a, 'm) mapFold = __ -> ('k -> 'a -> __ -> __) -> __ -> 'm -> __

val map_fold :
  ('a1, 'a2, 'a3) mapFold -> ('a1 -> 'a2 -> 'a4 -> 'a4) -> 'a4 -> 'a3 -> 'a4

val map_insert : ('a1, 'a2, 'a3) partialAlter -> ('a1, 'a2, 'a3) insert

val map_singleton :
  ('a1, 'a2, 'a3) partialAlter -> 'a3 empty -> ('a1, 'a2, 'a3) singletonM

val map_to_list : ('a1, 'a2, 'a3) mapFold -> 'a3 -> ('a1, 'a2) prod list

val map_union_with : 'a1 merge -> ('a2, 'a1) unionWith

val map_difference_with : 'a1 merge -> ('a2, 'a1) differenceWith

val map_union : 'a1 merge -> 'a1 union

val map_difference : 'a1 merge -> 'a1 difference

type 'munit mapset' =
  'munit
  (* singleton inductive, whose constructor was Mapset *)

val mapset_empty : (__ -> 'a1 empty) -> 'a1 mapset' empty

val mapset_singleton :
  (__ -> 'a2 empty) -> (__ -> ('a1, __, 'a2) partialAlter) -> ('a1, 'a2
  mapset') singleton

val mapset_union : 'a1 merge -> 'a1 mapset' union

val mapset_difference : 'a1 merge -> 'a1 mapset' difference

val mapset_elements :
  (__ -> ('a1, __, 'a2) mapFold) -> ('a1, 'a2 mapset') elements

val mapset_dom : 'a1 fMap -> ('a1, 'a1 mapset') dom

type 'a gmap_dep_ne =
| GNode001 of 'a gmap_dep_ne
| GNode010 of 'a
| GNode011 of 'a * 'a gmap_dep_ne
| GNode100 of 'a gmap_dep_ne
| GNode101 of 'a gmap_dep_ne * 'a gmap_dep_ne
| GNode110 of 'a gmap_dep_ne * 'a
| GNode111 of 'a gmap_dep_ne * 'a * 'a gmap_dep_ne

type 'a gmap_dep =
| GEmpty
| GNodes of 'a gmap_dep_ne

type ('k, 'a) gmap =
  'a gmap_dep
  (* singleton inductive, whose constructor was GMap *)

val gNode :
  'a1 gmap_dep -> (__, 'a1) prod option -> 'a1 gmap_dep -> 'a1 gmap_dep

val gmap_dep_ne_case :
  'a1 gmap_dep_ne -> ('a1 gmap_dep -> (__, 'a1) prod option -> 'a1 gmap_dep
  -> 'a2) -> 'a2

val gmap_dep_ne_lookup : positive -> 'a1 gmap_dep_ne -> 'a1 option

val gmap_dep_lookup : positive -> 'a1 gmap_dep -> 'a1 option

val gmap_lookup :
  ('a1, 'a1) relDecision -> 'a1 countable -> ('a1, 'a2, ('a1, 'a2) gmap)
  lookup

val gmap_empty :
  ('a1, 'a1) relDecision -> 'a1 countable -> ('a1, 'a2) gmap empty

val gmap_dep_ne_singleton : positive -> 'a1 -> 'a1 gmap_dep_ne

val gmap_partial_alter_aux :
  (positive -> __ -> 'a1 gmap_dep_ne -> 'a1 gmap_dep) -> ('a1 option -> 'a1
  option) -> positive -> 'a1 gmap_dep -> 'a1 gmap_dep

val gmap_dep_ne_partial_alter :
  ('a1 option -> 'a1 option) -> positive -> 'a1 gmap_dep_ne -> 'a1 gmap_dep

val gmap_dep_partial_alter :
  ('a1 option -> 'a1 option) -> positive -> 'a1 gmap_dep -> 'a1 gmap_dep

val gmap_partial_alter :
  ('a1, 'a1) relDecision -> 'a1 countable -> ('a1, 'a2, ('a1, 'a2) gmap)
  partialAlter

val gmap_dep_ne_fmap : ('a1 -> 'a2) -> 'a1 gmap_dep_ne -> 'a2 gmap_dep_ne

val gmap_dep_fmap : ('a1 -> 'a2) -> 'a1 gmap_dep -> 'a2 gmap_dep

val gmap_fmap :
  ('a1, 'a1) relDecision -> 'a1 countable -> (__ -> __) -> ('a1, __) gmap ->
  ('a1, __) gmap

val gmap_dep_omap_aux :
  ('a1 gmap_dep_ne -> 'a2 gmap_dep) -> 'a1 gmap_dep -> 'a2 gmap_dep

val gmap_dep_ne_omap : ('a1 -> 'a2 option) -> 'a1 gmap_dep_ne -> 'a2 gmap_dep

val gmap_merge_aux :
  ('a1 gmap_dep_ne -> 'a2 gmap_dep_ne -> 'a3 gmap_dep) -> ('a1 option -> 'a2
  option -> 'a3 option) -> 'a1 gmap_dep -> 'a2 gmap_dep -> 'a3 gmap_dep

val diag_None' :
  ('a1 option -> 'a2 option -> 'a3 option) -> (__, 'a1) prod option -> (__,
  'a2) prod option -> (__, 'a3) prod option

val gmap_dep_ne_merge :
  ('a1 option -> 'a2 option -> 'a3 option) -> 'a1 gmap_dep_ne -> 'a2
  gmap_dep_ne -> 'a3 gmap_dep

val gmap_dep_merge :
  ('a1 option -> 'a2 option -> 'a3 option) -> 'a1 gmap_dep -> 'a2 gmap_dep ->
  'a3 gmap_dep

val gmap_merge :
  ('a1, 'a1) relDecision -> 'a1 countable -> (__ option -> __ option -> __
  option) -> ('a1, __) gmap -> ('a1, __) gmap -> ('a1, __) gmap

val gmap_fold_aux :
  (positive -> 'a2 -> 'a1 gmap_dep_ne -> 'a2) -> positive -> 'a2 -> 'a1
  gmap_dep -> 'a2

val gmap_dep_ne_fold :
  (positive -> 'a1 -> 'a2 -> 'a2) -> positive -> 'a2 -> 'a1 gmap_dep_ne -> 'a2

val gmap_dep_fold :
  (positive -> 'a1 -> 'a2 -> 'a2) -> positive -> 'a2 -> 'a1 gmap_dep -> 'a2

val gmap_fold :
  ('a1, 'a1) relDecision -> 'a1 countable -> ('a1 -> 'a2 -> __ -> __) -> __
  -> ('a1, 'a2) gmap -> __

type 'k gset = ('k, unit0) gmap mapset'

val gset_empty : ('a1, 'a1) relDecision -> 'a1 countable -> 'a1 gset empty

val gset_singleton :
  ('a1, 'a1) relDecision -> 'a1 countable -> ('a1, 'a1 gset) singleton

val gset_union : ('a1, 'a1) relDecision -> 'a1 countable -> 'a1 gset union

val gset_difference :
  ('a1, 'a1) relDecision -> 'a1 countable -> 'a1 gset difference

val gset_elements :
  ('a1, 'a1) relDecision -> 'a1 countable -> ('a1, 'a1 gset) elements

val gset_dom :
  ('a1, 'a1) relDecision -> 'a1 countable -> (('a1, 'a2) gmap, 'a1 gset) dom

type nid = nat

type oid = nat

type val0 = z

type fresh1 =
| Clean
| Check
| Dirty

val stale : fresh1 -> bool

val fresh_eq_dec : (fresh1, fresh1) relDecision

type var = nat

type expr =
| EConst of val0
| EVar of var
| EPrev
| EAdd of expr * expr
| ESub of expr * expr
| EMul of expr * expr
| EDiv of expr * expr
| ELt of expr * expr
| EEq of expr * expr

type outcome =
| Ok of val0
| Fail of val0

type cmp =
| CmpEq
| CmpNever
| CmpThrows of val0

type scope_mode =
| Pure
| Owning

type cmd =
| CRet of expr
| CThrow of val0
| CLet of var * expr * cmd
| CIf of expr * cmd * cmd
| CRead of var * expr * cmd
| CTryRead of var * var * expr * cmd
| CReadU of var * expr * cmd
| CWrite of expr * expr * cmd
| CSignal of var * expr * cmd
| CMemo of var * scope_mode * cmp * cmd * cmd
| CEffect of var * cmd * cmd
| CCleanup of cmd * cmd
| CDispose of expr * cmd
| CBatch of cmd * cmd
| CLog of expr * cmd

type env = (var, val0) prod list

val env_lookup : env -> var -> val0 option

val bool_val : bool -> val0

val div_zero_failure : val0

val eval_expr : env -> val0 option -> expr -> outcome option

type event =
| EvRun of nid
| EvLog of nid * val0
| EvCleanup of nid
| EvFail of nid * val0
| EvDispose of nid
| EvHostRead of nid * outcome

val scan_limit : nat

type obs = { oitems : (nid, nat) prod list; olatest : nat }

val empty_obs : obs

type tried =
| TAdded of nat
| TUnlinked
| TUnknown

val obs_view : obs -> nid list

val obs_add : nid -> nat -> obs -> (obs, nat) prod

val obs_try_add : nid -> nat -> obs -> (obs, tried) prod

val obs_remove_at :
  nid -> nat -> obs -> (obs, ((nid, nat) prod, nat) prod option) prod option

type src = { sslots : (nid, nat option) prod list; scursor : nat }

type edgeWorld = { ew_obs : (nid, obs) gmap; ew_src : (nid, src) gmap }

val empty_src : src

val empty_world : edgeWorld

val obs_of : edgeWorld -> nid -> obs

val src_of : edgeWorld -> nid -> src

val slot_at : edgeWorld -> nid -> nat -> (nid, nat option) prod option

val set_obs : nid -> obs -> edgeWorld -> edgeWorld

val set_src : nid -> src -> edgeWorld -> edgeWorld

val sl_begin : nid -> edgeWorld -> edgeWorld

val set_handle : nid -> nat -> nat option -> edgeWorld -> edgeWorld

val sl_moved_in : nid -> nat -> nat -> edgeWorld -> edgeWorld

val remove_edge : nid -> nid -> nat -> edgeWorld -> edgeWorld option

val sl_remove_slot : nid -> nat -> edgeWorld -> edgeWorld option

val trim_loop : nid -> nat -> nat -> edgeWorld -> edgeWorld option

val sl_trim : nid -> nat -> edgeWorld -> edgeWorld option

val sl_end : nid -> edgeWorld -> edgeWorld option

val sl_clear : nid -> edgeWorld -> edgeWorld option

val sl_append : nid -> nid -> nat option -> obs -> edgeWorld -> edgeWorld

val sl_link : nid -> nid -> edgeWorld -> edgeWorld

val sl_add : nid -> nid -> edgeWorld -> edgeWorld option

type kind =
| KSignal
| KMemo
| KEffect

type status =
| StNone
| StPending
| StError
| StUninitialized

val status_eq_dec : (status, status) relDecision

type link = { l_child : nid; l_owner : oid; l_gen : nat option;
              l_acquired : nat }

type ownerS = { o_children : link list; o_cleanups : cmd list;
                o_cleanup_envs : env list; o_cleanup_order : nat list;
                o_acquisitions : nat; o_gen : nat; o_disposed : bool;
                o_sink : oid option; o_errors : val0 list; o_host : nid }

type node = { n_kind : kind; n_value : val0; n_failure : val0 option;
              n_status : status; n_fresh : fresh1; n_unann : bool;
              n_runs : nat; n_published : bool; n_disposed : bool;
              n_queued : bool; n_in_cleanup : bool; n_scope : oid option;
              n_violated : bool; n_body : cmd; n_env : env;
              n_mode : scope_mode; n_cmp : cmp; n_link : link option }

type flags = { f_unannounced : bool; f_reader_mark : bool }

val current_flags : flags

type nview = ((val0, status) prod, val0 option) prod

val nview_of : node -> nview

type ghost = { gh_pub : (nid, nview) gmap;
               gh_seen : ((nid, nid) prod, nview) gmap;
               gh_unnoticed : nid gset }

val empty_ghost : ghost

type state = { g_flags : flags; g_edges : edgeWorld;
               g_nodes : (nid, node) gmap; g_owners : (oid, ownerS) gmap;
               g_queue : nid list; g_head : nat; g_flushing : bool;
               g_owed : bool; g_batch : nat; g_pull : nat;
               g_current : nid option; g_untracked : bool;
               g_owner : oid option; g_next : nat; g_trace : event list;
               g_running : nid list; g_ghost : ghost }

val node_marked : fresh1 -> bool -> bool -> node -> node

val set_node : nid -> node -> state -> state

val enqueue : nid -> state -> state

val walk_marks :
  (nid -> state -> state option) -> nid -> nat -> state -> state option

val notify_with :
  (nid -> state -> state option) -> nid -> state -> state option

val announce_due : state -> node -> bool

val effect_marked : fresh1 -> nid -> node -> state -> state

val mark_check : nat -> nid -> state -> state option

val mark_dirty : nat -> nid -> state -> state option

val notify_dirty : nat -> nid -> state -> state option

val mark_except : nat -> nid option -> nid -> state -> state option

val notify_dirty_except : nat -> nid -> nid option -> state -> state option

val pending : state -> nid list

type 'a m = state -> (state, 'a) prod option

val m_ret : 'a1 -> 'a1 m

val m_bind : 'a1 m -> ('a1 -> 'a2 m) -> 'a2 m

val stuck : 'a1 m

val m_get : state m

val m_mod : (state -> state) -> unit0 m

val m_step : (state -> state option) -> unit0 m

val with_edges : edgeWorld -> state -> state

val with_owners : (oid, ownerS) gmap -> state -> state

val with_queue : nid list -> state -> state

val with_head : nat -> state -> state

val with_flushing : bool -> state -> state

val with_owed : bool -> state -> state

val with_batch : nat -> state -> state

val with_pull : nat -> state -> state

val with_current : nid option -> state -> state

val with_untracked : bool -> state -> state

val with_owner : oid option -> state -> state

val with_next : nat -> state -> state

val with_trace : event list -> state -> state

val with_running : nid list -> state -> state

val with_ghost : ghost -> state -> state

val nset_value : val0 -> node -> node

val nset_failure : val0 option -> node -> node

val nset_status : status -> node -> node

val nset_fresh : fresh1 -> node -> node

val nset_unann : bool -> node -> node

val nset_runs : nat -> node -> node

val nset_published : bool -> node -> node

val nset_disposed : bool -> node -> node

val nset_queued : bool -> node -> node

val nset_in_cleanup : bool -> node -> node

val nset_scope : oid option -> node -> node

val nset_violated : bool -> node -> node

val nset_link : link option -> node -> node

val emit : event -> unit0 m

val get_node : nid -> node m

val mod_node : nid -> (node -> node) -> unit0 m

val get_owner : oid -> ownerS m

val put_owner : oid -> ownerS -> unit0 m

val edges : (edgeWorld -> edgeWorld) -> unit0 m

val edges_opt : (edgeWorld -> edgeWorld option) -> unit0 m

val push_running : nid -> unit0 m

val pop_running : unit0 m

val read_prefix : edgeWorld -> nid -> nid list

val ghost_read : nid -> nid -> state -> state

val ghost_run_start : nid -> state -> state

val ghost_publish : nid -> state -> state

val run_end_unnoticed : nid -> nid option -> bool -> node -> state -> nid gset

val ghost_run_end : nid -> nid option -> bool -> state -> state

val ghost_dirty_cleared : nid -> state -> state

val root_oid : oid

val new_owner : nid -> oid option -> bool -> ownerS

val owner_with_child : link -> ownerS -> ownerS

val owner_with_cleanup : cmd -> env -> ownerS -> ownerS

val zip_cleanups :
  cmd list -> env list -> nat list -> ((cmd, env) prod, nat) prod list

val owner_cleanups : ownerS -> ((cmd, env) prod, nat) prod list

val owner_detached : ownerS -> ownerS

val owner_retired : ownerS -> ownerS

val owner_with_error : val0 -> ownerS -> ownerS

val owner_without : link -> ownerS -> ownerS

val unlink : link -> unit0 m

val record_error : oid -> val0 -> unit0 m

val log_label : nid m

val pure_scope_failure : val0

val prev_var : var

val without_prev : env -> env

val detached : oid -> unit0 m -> unit0 m

val hosted : nid -> 'a1 m -> 'a1 m

val untracked : 'a1 m -> 'a1 m

val deferring : state -> bool

val live_queue : state -> bool

val outside_bodies : state -> bool

val stale_live : node -> bool

val track : nid -> unit0 m

val mark_reader : nat -> unit0 m

val on_value : env -> expr -> (val0 -> outcome m) -> outcome m

val on_node : env -> expr -> (nid -> outcome m) -> outcome m

val note_host_read : nid -> outcome -> unit0 m

val next_id : nid m

val signal_node : val0 -> node

val memo_node : scope_mode -> cmp -> cmd -> env -> node

val effect_node : cmd -> env -> node

val add_node : nid -> node -> unit0 m

val current_owner : oid option m

val exec : nat -> env -> cmd -> outcome m

val read : nat -> nid -> bool -> outcome m

val read_node : nat -> nid -> outcome m

val update_if_necessary : nat -> nid -> unit0 m

val refresh_or_pull : nat -> nid -> unit0 m

val pull : nat -> nid -> unit0 m

val refresh : nat -> nid -> unit0 m

val resolve_walk : nat -> nid -> nat -> unit0 m

val recompute : nat -> nid -> unit0 m

val run_memo : nat -> nid -> unit0 m

val execute_effect : nat -> nid -> unit0 m

val run_effect : nat -> nid -> unit0 m

val run_effect_body : nat -> nid -> unit0 m

val discharge : nat -> oid -> unit0 m

val retire : nat -> oid -> unit0 m

val dispose_scope : nat -> oid -> unit0 m

val teardown :
  nat -> oid -> link list -> ((cmd, env) prod, nat) prod list -> unit0 m

val run_cleanup : nat -> oid -> cmd -> env -> unit0 m

val release : nat -> nid -> unit0 m

val dispose_node : nat -> nid -> unit0 m

val attach : nat -> nid -> oid -> unit0 m

val write_signal : nat -> nid -> val0 -> unit0 m

val request_flush : nat -> unit0 m

val settle_flush : nat -> unit0 m

val run_flush : nat -> unit0 m

val drain : nat -> unit0 m

val init : flags -> state

val run_top : nat -> cmd -> outcome m

val trace_of : nat -> flags -> cmd -> event list option

type node_view = { v_id : nid; v_kind : kind; v_value : val0;
                   v_status : status; v_failure : val0 option;
                   v_fresh : fresh1; v_unann : bool; v_runs : nat;
                   v_disposed : bool; v_queued : bool; v_sources : nat;
                   v_observers : nat }

type state_view = { v_nodes : node_view list; v_queue : nat; v_errors : nat }

val view_node : state -> nid -> node -> node_view

val node_ids : state -> nid list

val view_of : state -> state_view

val memo_ids : state -> nid list

val closing : nat -> unit0 m

val observed :
  nat -> flags -> cmd -> ((event list, state_view) prod, state_view) prod
  option
