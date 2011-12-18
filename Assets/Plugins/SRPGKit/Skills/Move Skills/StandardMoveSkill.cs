using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class StandardMoveSkill : MoveSkill {
	public bool drawPath=true;
	public bool useOnlyOneWaypoint=false;
	
	public Material pathMaterial;

	public Vector3 moveDest=Vector3.zero;
	[HideInInspector]
	[SerializeField]
	Vector3 initialPosition=Vector3.zero;
	
	[HideInInspector]
	[SerializeField]
	int nodeCount=0;

	[HideInInspector]
	[SerializeField]
	PathNode endOfPath;
	[HideInInspector]
	[SerializeField]
	List<PathNode> waypoints;
	[HideInInspector]
	public LineRenderer lines;
	
	//for path-drawing
	public float newNodeThreshold=0.05f;
	public float NewNodeThreshold { get { return lockToGrid ? 1 : newNodeThreshold; } }
	
	[HideInInspector]
	public float xyRangeSoFar=0;
	
	public bool waypointsAreIncremental=true;

	public bool immediatelyFollowDrawnPath=false;
	public bool canCancelMovement=true;
	
	protected override PathNode[] GetValidActionTiles() {
		if(!lockToGrid) { return null; }
		return Strategy.GetValidMoves(
			moveDest, 
			0, Strategy.xyRangeMax-xyRangeSoFar, 
			0, Strategy.zRangeDownMax, 
			0, Strategy.zRangeUpMax
		);
	}
	
	//for some reason, putting UpdateParameters inside of CreateOverlay -- even with
	//checks to see if the overlay already existed -- caused horrible unity crashers.
	
	protected void UpdateOverlayParameters() {
		if(overlay == null) { return; }
		if(lockToGrid) {
			_GridOverlay.UpdateDestinations(GetValidActionTiles());
		} else {
			Vector3 charPos = moveDest;
			_RadialOverlay.tileRadius = (Strategy.xyRangeMax - xyRangeSoFar);
			_RadialOverlay.UpdateOriginAndRadius(
				map.TransformPointWorld(charPos), 
				(Strategy.xyRangeMax - xyRangeSoFar)*map.sideLength
			);
		}
	}
	
	protected override void CreateOverlay() {
		if(overlay != null) { return; }
		//N.B.: do not call base implementation atm
		if(lockToGrid) {
			PathNode[] destinations = GetValidActionTiles();
			overlay = map.PresentGridOverlay(
				skillName, character.gameObject.GetInstanceID(), 
				overlayColor,
				highlightColor,
				destinations
			);
		} else {
			Vector3 charPos = moveDest;
			if(overlayType == RadialOverlayType.Sphere) {
				overlay = map.PresentSphereOverlay(
					skillName, character.gameObject.GetInstanceID(), 
					overlayColor,
					charPos,
					Strategy.xyRangeMax - xyRangeSoFar,
					drawOverlayRim,
					drawOverlayVolume,
					invertOverlay
				);
			} else if(overlayType == RadialOverlayType.Cylinder) {
				overlay = map.PresentCylinderOverlay(
					skillName, character.gameObject.GetInstanceID(), 
					overlayColor,
					charPos,
					Strategy.xyRangeMax - xyRangeSoFar,
					Strategy.zRangeDownMax,
					drawOverlayRim,
					drawOverlayVolume,
					invertOverlay
				);
			}
		}
	}
	
	override public void ActivateSkill() {
		targetingMode = TargetingMode.Custom;
		moveDest = character.TilePosition;
		initialPosition = moveDest;
		xyRangeSoFar = 0;
		nodeCount = 0;
		endOfPath = null;
		base.ActivateSkill();
	}
	
	override public void DeactivateSkill() {
		base.DeactivateSkill();
		Object.Destroy(probe.gameObject);
		endOfPath = null;
		waypoints = null;
		xyRangeSoFar = 0;
		nodeCount = 0;
		awaitingConfirmation=false;
	}
	
	public override void ResetActionSkill() {
		base.ResetActionSkill();
		targetingMode = TargetingMode.Custom;
	}
	
	protected bool DestIsBacktrack(Vector3 newDest) {
		return !immediatelyFollowDrawnPath && drawPath && (
		(endOfPath != null && endOfPath.prev != null && newDest == endOfPath.prev.pos) ||
		(!waypointsAreIncremental && waypoints.Count > 0 &&
			(((endOfPath.prev == null) && 
			(waypoints[waypoints.Count-1].pos == newDest)) ||
			
			(endOfPath.prev == null &&
			waypoints[waypoints.Count-1].prev != null &&
			newDest == waypoints[waypoints.Count-1].prev.pos)
			)));
	}

	protected override void ActivateTargetCustom() {
	}
	protected override void UpdateTargetCustom() {
		if(supportMouse) {
			if(Input.GetMouseButton(0)) {
				Ray r = Camera.main.ScreenPointToRay(Input.mousePosition);
				Vector3 hitSpot;
				bool inside = overlay.Raycast(r, out hitSpot);
				PathNode pn = overlay.PositionAt(hitSpot);
				if(lockToGrid) {
					hitSpot.x = Mathf.Floor(hitSpot.x+0.5f);
					hitSpot.y = Mathf.Floor(hitSpot.y+0.5f);
					hitSpot.z = Mathf.Floor(hitSpot.z+0.5f);
				}
				if(inside && (!(drawPath || immediatelyFollowDrawnPath) || pn != null)) {
					//TODO: better drag controls
//					if(drawPath && dragging) {
						//draw path: drag
						//unwind drawn path: drag backwards
//					}
					
					if(!(drawPath || immediatelyFollowDrawnPath)) {
						UpdatePath(hitSpot);
					} else {
						Vector3 srcPos = endOfPath.pos;
						//add positions from pn back to current pos
						List<Vector3> pts = new List<Vector3>();
						while(pn != null && pn.pos != srcPos) {
							pts.Add(pn.pos);
							pn = pn.prev;
						}
						for(int i = 0; i < pts.Count; i++) {
							UpdatePath(pts[i]);
						}
					}
					if(Input.GetMouseButtonDown(0)) {
						if(Time.time-firstClickTime > doubleClickThreshold) {
							firstClickTime = Time.time;
						} else {
							firstClickTime = -1;
							if(!waypointsAreIncremental && !immediatelyFollowDrawnPath &&
									waypoints.Count > 0 && 
									waypoints[waypoints.Count-1].pos == hitSpot) {
								UnwindToLastWaypoint();
							} else {
								if(overlay.ContainsPosition(hitSpot)) {
									ConfirmWaypoint();
								}
							}
						}
					}
				}
			}
		}
		float h = Input.GetAxis("Horizontal");
		float v = Input.GetAxis("Vertical");
		if(supportKeyboard && (h != 0 || v != 0)) {
			if(lockToGrid) {
			  if((Time.time-lastIndicatorKeyboardMove) > indicatorKeyboardMoveThreshold) {
					Vector2 d = map.TransformKeyboardAxes(h, v);
					if(Mathf.Abs(d.x) > Mathf.Abs(d.y)) { d.x = Mathf.Sign(d.x); d.y = 0; }
					else { d.x = 0; d.y = Mathf.Sign(d.y); }
					Vector3 newDest = moveDest;
					if(newDest.x+d.x >= 0 && newDest.y+d.y >= 0 &&
					 	 map.HasTileAt((int)(newDest.x+d.x), (int)(newDest.y+d.y))) {
						lastIndicatorKeyboardMove = Time.time;
						newDest.x += d.x;
						newDest.y += d.y;
						newDest.z = map.NearestZLevel((int)newDest.x, (int)newDest.y, (int)newDest.z);
						if(DestIsBacktrack(newDest)) {
							UnwindPath();
						} else {
							PathNode pn = overlay.PositionAt(newDest);
							if(!(drawPath || immediatelyFollowDrawnPath) || (pn != null && pn.canStop)) {
								UpdatePath(newDest);
							}
						}
					}
				}
			} else {
				Transform cameraTransform = Camera.main.transform;
				Vector3 forward = cameraTransform.TransformDirection(Vector3.forward);
				forward.y = 0;
				forward = forward.normalized;
				Vector3 right = new Vector3(forward.z, 0, -forward.x);
				Vector3 offset = h * right + v * forward;
				
				//try to move the probe
				Vector3 lastProbePos = probe.transform.position;
				probe.SimpleMove(offset*keyboardMoveSpeed);
				
				Vector3 newDest = map.InverseTransformPointWorld(probe.transform.position);
				PathNode pn = overlay.PositionAt(newDest);
				float thisDistance = Vector3.Distance(newDest, moveDest);
				if(thisDistance >= NewNodeThreshold) {
					if(!(drawPath || immediatelyFollowDrawnPath) || (pn != null && pn.canStop)) {
						if(drawPath) {
							lines.SetPosition(nodeCount, probe.transform.position);
						}
						UpdatePath(newDest);
					} else {
						probe.transform.position = lastProbePos;
					}
				} else {
					if(drawPath && pn != null && pn.canStop) {
						lines.SetPosition(nodeCount, probe.transform.position);
					}
				}
			}
		}
		if(supportKeyboard && Input.GetButtonDown("Confirm")) {
			if(requireConfirmation && !awaitingConfirmation) {
				awaitingConfirmation = true;
				if(performTemporaryMoves) {
					TemporaryMoveToPathNode(endOfPath);
				}
			} else {
				ConfirmWaypoint();
			}
		}
	}
	protected override void PresentMovesCustom() {
	}
	protected override void DeactivateTargetCustom() {
	}
	protected override void CancelTargetCustom() {
		if(canCancelMovement) {
			if(requireConfirmation && awaitingConfirmation) {
				awaitingConfirmation = false;
				if(performTemporaryMoves) {
					TemporaryMove(Executor.position);
				}
				ResetPosition();
			} else if(waypoints.Count > 0 && !waypointsAreIncremental && !immediatelyFollowDrawnPath) {
				UnwindToLastWaypoint();
			} else if(endOfPath == null || endOfPath.prev == null) {
				Cancel();
			} else {
				ResetPosition();
			}
		} else {
			PerformMoveToPathNode(endOfPath);
		}
	}
	
	protected bool PermitsNewWaypoints { get {
		if(immediatelyFollowDrawnPath) { return false; }
		if(useOnlyOneWaypoint) { return false; }
		return ((xyRangeSoFar + newNodeThreshold) < XYRange);
	} }
	
	protected void ConfirmWaypoint() {
		if(!drawPath) {
			endOfPath = overlay.PositionAt(moveDest);
			xyRangeSoFar += endOfPath.xyDistanceFromStart;
		}
		if(performTemporaryMoves) {
			TemporaryMoveToPathNode(endOfPath);
		}
		awaitingConfirmation = false;
		if(!PermitsNewWaypoints) {
			if(!waypointsAreIncremental) {
				PathNode p = endOfPath;
				int tries = 0;
				const int tryLimit = 1000;
				while((p.prev != null || waypoints.Count > 0) && tries < tryLimit) {
					tries++;
					if(p.prev == null) {
						p.prev = waypoints[waypoints.Count-1];
						waypoints.RemoveAt(waypoints.Count-1);
					} else {
						p = p.prev;
					}
					while(p.prev != null && p.pos == p.prev.pos) {
						p.prev = p.prev.prev;
					}
				}
				if(tries >= tryLimit) {
					Debug.LogError("caught infinite node loop");
				} 
			}
			if(immediatelyFollowDrawnPath) {
				endOfPath = new PathNode(moveDest, null, 0);
			}
			PerformMoveToPathNode(endOfPath);			
		} else {
			if(immediatelyFollowDrawnPath) {
				IncrementalMoveToPathNode(new PathNode(endOfPath.pos, null, 0));
			} else if(waypointsAreIncremental) {
				IncrementalMoveToPathNode(endOfPath);
			} else {
				TemporaryMoveToPathNode(endOfPath);
			}
			waypoints.Add(endOfPath);
			endOfPath = new PathNode(endOfPath.pos, null, xyRangeSoFar);
			UpdateOverlayParameters();
		}
	}
	
	protected void UpdatePath(Vector3 newDest, bool backwards=false) {
		float thisDistance = Vector3.Distance(newDest, moveDest);
		if(lockToGrid) {
			thisDistance = (int)thisDistance;
		}
		moveDest = newDest;
		if(!drawPath) {
			endOfPath = new PathNode(moveDest, null, 0);
		} else {
			xyRangeSoFar += thisDistance;
			endOfPath = new PathNode(moveDest, endOfPath, xyRangeSoFar);
			//add a line to this point
			nodeCount += 1;
			lines.SetVertexCount(nodeCount+1);
			lines.SetPosition(nodeCount, map.TransformPointWorld(moveDest));
			if(performTemporaryMoves) {
				TemporaryMoveToPathNode(endOfPath);
			}
		}
		if(immediatelyFollowDrawnPath) {
			IncrementalMove(newDest);
		}
		if(drawPath) {
			//update the overlay
			UpdateOverlayParameters();
			if(lockToGrid) {
				Vector4[] selPts = _GridOverlay.selectedPoints ?? new Vector4[0];
				_GridOverlay.SetSelectedPoints(selPts.Concat(
					new Vector4[]{new Vector4(newDest.x, newDest.y, newDest.z, 1)}
				).ToArray());
			}
		} else {
			if(lockToGrid) {
				_GridOverlay.SetSelectedPoints(
					new Vector4[]{new Vector4(newDest.x, newDest.y, newDest.z, 1)}
				);
			}
		}
		probe.transform.position = map.TransformPointWorld(moveDest);
	}
	
	public void UnwindToLastWaypoint() {
		int priorCount = waypoints.Count;
		if(priorCount == 0) {
			ResetPosition();
		} else {
			while(waypoints.Count == priorCount) {
				UnwindPath(1);
			}
		}
	}
	
	protected bool CanUnwindPath { get { 
		return !immediatelyFollowDrawnPath && 
		(endOfPath.prev != null || (!waypointsAreIncremental && waypoints.Count > 0));
	} }
	public void UnwindPath(int nodes=1) {
		for(int i = 0; i < nodes && CanUnwindPath; i++) {
			Vector3 oldEnd = endOfPath.pos;
			PathNode prev = (endOfPath != null && endOfPath.prev != null) ? 
				endOfPath.prev : 
				(waypoints.Count > 0 ? waypoints[waypoints.Count-1] : null);
			float thisDistance = Vector2.Distance(
				new Vector2(oldEnd.x, oldEnd.y), 
				new Vector2(prev.pos.x, prev.pos.y)
			);
			if(lockToGrid) {
				thisDistance = (int)thisDistance;
				Vector4[] selPts = _GridOverlay.selectedPoints ?? new Vector4[0];
				_GridOverlay.SetSelectedPoints(selPts.Except(
					new Vector4[]{new Vector4(oldEnd.x, oldEnd.y, oldEnd.z, 1)}
				).ToArray());
			}
			if(drawPath) {
				xyRangeSoFar -= thisDistance;
			}
			endOfPath = endOfPath.prev;
			if((endOfPath == null || endOfPath.prev == null) && waypoints.Count > 0 && !waypointsAreIncremental) {
				if(endOfPath == null) {
					PathNode wp=waypoints[waypoints.Count-1], wpp=wp.prev;
					if(drawPath) {
						endOfPath = wp.prev;
						thisDistance = Vector2.Distance(
							new Vector2(wp.pos.x, wp.pos.y), 
							new Vector2(wpp.pos.x, wpp.pos.y)
						);
					} else {
						//either waypoint-2 or start
						if(waypoints.Count > 1) {
							endOfPath = waypoints[waypoints.Count-2];
						} else {
							endOfPath = new PathNode(initialPosition, null, 0);
						}
						moveDest = endOfPath.pos;
						thisDistance = wp.xyDistanceFromStart;
					}
					if(lockToGrid) { thisDistance = (int)thisDistance; }
					xyRangeSoFar -= thisDistance;						
				} else {
					endOfPath = waypoints[waypoints.Count-1];
				}
				waypoints.RemoveAt(waypoints.Count-1);
				PathNode startOfPath = endOfPath;
				while(startOfPath.prev != null) {
					startOfPath = startOfPath.prev;
				}
				TemporaryMoveToPathNode(startOfPath);
			}
			nodeCount -= 1;
			moveDest = endOfPath.pos;
			if(performTemporaryMoves) {
				TemporaryMoveToPathNode(endOfPath);
			}
			probe.transform.position = map.TransformPointWorld(moveDest);
			if(drawPath) {
				lines.SetVertexCount(nodeCount+1);
				lines.SetPosition(nodeCount, probe.transform.position);
			}
		}
		//update the overlay
		UpdateOverlayParameters();	
	}
	
	override public void PresentMoves() {
		initialPosition = character.TilePosition;
		moveDest = initialPosition;
		probe = Object.Instantiate(probePrefab, Vector3.zero, Quaternion.identity) as CharacterController;
		probe.transform.parent = map.transform;
		Physics.IgnoreCollision(probe.collider, character.collider);
		waypoints = new List<PathNode>();
		if(drawPath) {
			lines = probe.gameObject.AddComponent<LineRenderer>();
			lines.materials = new Material[]{pathMaterial};
			lines.useWorldSpace = true;
		}
		ResetPosition();
		base.PresentMoves();
	}
	
	protected void ResetPosition() {
		if(waypoints.Count > 0 && !waypointsAreIncremental && !immediatelyFollowDrawnPath) {
			UnwindToLastWaypoint();
		} else {
			Vector3 tp = initialPosition;
			if(lockToGrid) {
				tp.x = (int)Mathf.Round(tp.x);
				tp.y = (int)Mathf.Round(tp.y);
				tp.z = map.NearestZLevel((int)tp.x, (int)tp.y, (int)Mathf.Round(tp.z));
			}
			probe.transform.position = map.TransformPointWorld(tp);
			moveDest = initialPosition;
			xyRangeSoFar = 0;
			if(drawPath) {
				endOfPath = new PathNode(tp, null, 0);
				lines.SetVertexCount(1);
				lines.SetPosition(0, probe.transform.position);
			} else {
				endOfPath = null;
			}
			UpdateOverlayParameters();
			if(overlay != null && lockToGrid) {
				_GridOverlay.SetSelectedPoints(new Vector4[0]);
			}
		}
	}	
	
}
