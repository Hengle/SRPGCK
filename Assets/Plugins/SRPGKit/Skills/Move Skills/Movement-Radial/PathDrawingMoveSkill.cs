using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum WaypointMode {
	Count,
	UpToXYRange
};

public class PathDrawingMoveSkill : MoveSkill {
	public float moveSpeed=10.0f;
	
	public bool drawPath=true;
	
	public Color overlayColor = new Color(0.3f, 0.3f, 0.3f, 0.7f);
	public Color highlightColor = new Color(0.5f, 0.5f, 0.5f, 1.0f);
	public Material pathMaterial;

	[SerializeField]
	Vector3 moveDest=Vector3.zero;
	[SerializeField]
	Vector3 initialPosition=Vector3.zero;
	
	[SerializeField]
	int nodeCount=0;

	[SerializeField]
	PathNode endOfPath;
	[SerializeField]
	List<PathNode> waypoints;
	
	public CharacterController probePrefab;
	[SerializeField]
	CharacterController probe;
	[HideInInspector]
	public LineRenderer lines;
	
	public float newNodeThreshold=0.05f;
	
	public float NewNodeThreshold { get { return lockToGrid ? 1 : newNodeThreshold; } }
	
	//if lockToGrid
	float lastIndicatorKeyboardMove=0;
	float indicatorKeyboardMoveThreshold=0.3f;

	//if !lockToGrid
	public RadialOverlayType overlayType = RadialOverlayType.Sphere;
	[SerializeField]
	Overlay overlay;
	//TODO: expose to editor
	public bool drawOverlayRim = false;
	public bool drawOverlayVolume = false;
	//TODO: support for grid-locking
	public bool invertOverlay = true; //draw an overlay on the map's exterior
	
	[HideInInspector]
	public float xyRangeSoFar=0;
	
	protected GridOverlay _GridOverlay { get { return overlay as GridOverlay; } }	
	protected RadialOverlay _RadialOverlay { get { return overlay as RadialOverlay; } }	
	
	public WaypointMode waypointMode=WaypointMode.Count;
	public int requiredWaypoints=1;
	public bool waypointsAreIncremental=true;
	
	protected void UpdateOverlay() {
		if(lockToGrid) {
			PathNode[] destinations = Strategy.GetValidMoves(
				moveDest, 
				0, Strategy.xyRangeMax-xyRangeSoFar, 
				0, Strategy.zRangeDownMax, 
				0, Strategy.zRangeUpMax
			);
			if(overlay != null) {
				_GridOverlay.UpdateDestinations(destinations);
			} else {
				overlay = map.PresentGridOverlay(
					skillName, character.gameObject.GetInstanceID(), 
					overlayColor,
					highlightColor,
					destinations
				);
			}
		} else {
			Vector3 charPos = moveDest;
			if(overlay != null) {
				_RadialOverlay.tileRadius = (Strategy.xyRangeMax - xyRangeSoFar);
				_RadialOverlay.UpdateOriginAndRadius(
					map.TransformPointWorld(charPos), 
					(Strategy.xyRangeMax - xyRangeSoFar)*map.sideLength
				);
			} else {
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
	}
	
	override public void ActivateSkill() {
		moveDest = character.TilePosition;
		initialPosition = moveDest;
		xyRangeSoFar = 0;
		xyRangeSoFar = 0;
		nodeCount = 0;
		endOfPath = null;
		awaitingConfirmation=false;
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
		if(map.IsShowingOverlay(skillName, character.gameObject.GetInstanceID())) {
			map.RemoveOverlay(skillName, character.gameObject.GetInstanceID());
		}	
		overlay = null;
	}
	
	protected bool DestIsBacktrack(Vector3 newDest) {
		return drawPath && (
		(endOfPath != null && endOfPath.prev != null && newDest == endOfPath.prev.pos) ||
		(!waypointsAreIncremental && waypoints.Count > 0 &&
			(((endOfPath.prev == null) && 
			(waypoints[waypoints.Count-1].pos == newDest)) ||
			
			(endOfPath.prev == null &&
			waypoints[waypoints.Count-1].prev != null &&
			newDest == waypoints[waypoints.Count-1].prev.pos)
			)));
	}
	
	override public void Update () {
		base.Update();
		if(character == null || !character.isActive) { return; }
		if(!isActive) { return; }
		if(!arbiter.IsLocalPlayer(character.EffectiveTeamID)) {
			return;
		}
		
/*		if(supportMouse && Input.GetMouseButton(0)) {
			Ray r = Camera.main.ScreenPointToRay(Input.mousePosition);
			Vector3 hitSpot;
			bool inside = overlay.Raycast(r, out hitSpot);
			if(inside && overlay.ContainsPosition(hitSpot)) {
				moveDest = hitSpot;
				//move the probe here
				IncrementalMove(moveDest);
				if(Input.GetMouseButtonDown(0)) {
					if(Time.time-firstClickTime > doubleClickThreshold) {
						firstClickTime = Time.time;
					} else  {
						firstClickTime = -1;
						PerformMove(moveDest);
					}
				}
			}
		}
		*/
		
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
							if(pn != null && pn.canStop) {
								probe.transform.position = map.TransformPointWorld(newDest);
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
				probe.SimpleMove(offset*moveSpeed);
				
				Vector3 newDest = map.InverseTransformPointWorld(probe.transform.position);
				PathNode pn = overlay.PositionAt(newDest);
				if(pn != null && pn.canStop) {
					if(drawPath) {
						lines.SetPosition(nodeCount, probe.transform.position);
					}
					float thisDistance = Vector3.Distance(newDest, moveDest);
					if(thisDistance >= NewNodeThreshold) {
						UpdatePath(newDest);
					}
				} else {
					probe.transform.position = lastProbePos;
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
		if(supportKeyboard && Input.GetButtonDown("Cancel")) {
			if(requireConfirmation && awaitingConfirmation) {
				awaitingConfirmation = false;
				if(performTemporaryMoves) {
					TemporaryMove(Executor.position);
				}
				ResetPosition();
			} else if(waypoints.Count > 0 && !waypointsAreIncremental) {
				UnwindToLastWaypoint();
			} else {
				Cancel();
			}
		}
	}
	
	protected bool AnyRemainingWaypoints { get {
		switch(waypointMode) {
			case WaypointMode.Count: return waypoints == null || (requiredWaypoints-1) > waypoints.Count;
			case WaypointMode.UpToXYRange: return xyRangeSoFar < XYRange;
		}
		return false;
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
		if(!AnyRemainingWaypoints) {
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
			PerformMoveToPathNode(endOfPath);			
		} else {
			if(waypointsAreIncremental) {
				IncrementalMoveToPathNode(endOfPath);
			} else {
				TemporaryMoveToPathNode(endOfPath);
			}
			waypoints.Add(endOfPath);
			endOfPath = new PathNode(endOfPath.pos, null, xyRangeSoFar);
			UpdateOverlay();
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
		if(drawPath) {
			//update the overlay
			UpdateOverlay();
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
		return endOfPath.prev != null || (!waypointsAreIncremental && waypoints.Count > 0);
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
		UpdateOverlay();	
	}
	
	override protected void PresentMoves() {
		base.PresentMoves();
		moveDest = character.TilePosition;
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
	}
	
	protected void ResetPosition() {
		if(waypoints.Count > 0 && !waypointsAreIncremental) {
			UnwindToLastWaypoint();
		} else {
			Vector3 tp = initialPosition;
			if(lockToGrid) {
				tp.x = (int)Mathf.Round(tp.x);
				tp.x = (int)Mathf.Round(tp.y);
				tp.z = map.NearestZLevel((int)tp.x, (int)tp.y, (int)Mathf.Round(tp.z));
			}
			probe.transform.position = map.TransformPointWorld(tp);
			if(drawPath) {
				endOfPath = new PathNode(tp, null, 0);
				lines.SetVertexCount(1);
				lines.SetPosition(0, probe.transform.position);
			} else {
				endOfPath = null;
			}
			UpdateOverlay();
			if(lockToGrid) {
				_GridOverlay.SetSelectedPoints(new Vector4[0]);
			}
		}
	}	
	
}
