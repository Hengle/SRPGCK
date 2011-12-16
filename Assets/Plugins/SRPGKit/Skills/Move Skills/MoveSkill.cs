using UnityEngine;

[System.Serializable]
public abstract class MoveSkill : Skill {
	public bool lockToGrid=true;
	
	public abstract MoveIO IO { get; }
	public bool supportKeyboard = true;	
	public bool supportMouse = true;
	public bool requireConfirmation = true;
	public float indicatorCycleLength=1.0f;
	
	public ActionStrategy Strategy { get { return moveStrategy; } }
	public MoveExecutor Executor { get { return moveExecutor; } }

	//strategy
	public ActionStrategy moveStrategy;
	public float ZDelta { get { return GetParam("range.z", character.GetStat("jump", 3)); } }
	public float XYRange { get { return GetParam("range.xy", character.GetStat("move", 3)); } }
	
	//executor
	[HideInInspector]
	public MoveExecutor moveExecutor;
	public bool performTemporaryMoves = false;
	public bool animateTemporaryMovement=false;
	public float XYSpeed = 12;
	public float ZSpeedUp = 15;
	public float ZSpeedDown = 20;
	
	protected abstract void MakeIO();
	
	public override void Start() {
		base.Start();
		isPassive = false;
		MakeIO();
		IO.lockToGrid = lockToGrid;
		IO.owner = this;
		IO.performTemporaryMoves = performTemporaryMoves;
		if(moveStrategy == null) {
			moveStrategy = new ActionStrategy();
		}
		moveExecutor = new MoveExecutor();
		Executor.lockToGrid = lockToGrid;
		Strategy.owner = this;
		Executor.owner = this;
	}
	
	public override void ActivateSkill() {
		base.ActivateSkill();
		if(IO == null) {
			MakeIO();
		}
		IO.owner = this;
		IO.supportKeyboard = supportKeyboard;
		IO.supportMouse = supportMouse;
		IO.requireConfirmation = requireConfirmation;
		IO.indicatorCycleLength = indicatorCycleLength;
		IO.performTemporaryMoves = performTemporaryMoves;
		IO.lockToGrid = lockToGrid;

		Strategy.owner = this;
		Strategy.zRangeDownMin = 0;
		Strategy.zRangeDownMax = ZDelta;
		Strategy.zRangeUpMin = 0;
		Strategy.zRangeUpMax = ZDelta;
		Strategy.xyRangeMin = 0;
		Strategy.xyRangeMax = XYRange;
	
		Executor.owner = this;	
		Executor.lockToGrid = lockToGrid;
		Executor.animateTemporaryMovement = animateTemporaryMovement;
		Executor.XYSpeed = XYSpeed;
		Executor.ZSpeedUp = ZSpeedUp;
		Executor.ZSpeedDown = ZSpeedDown;	
		Strategy.Activate();
		Executor.Activate();

		IO.Activate();		
		IO.PresentMoves();
	}	

	public override void DeactivateSkill() {
		if(!isActive) { return; }
		IO.Deactivate();
		Strategy.Deactivate();
		Executor.Deactivate();
		base.DeactivateSkill();
	}
	
	public override void Update() {
		base.Update();
		if(!isActive) { return; }
		IO.owner = this;
		Strategy.owner = this;
		Executor.owner = this;
		IO.Update();
		Strategy.Update();
		Executor.Update();	
	}

	public override void Reset() {
		base.Reset();
		skillName = "Move";
		skillSorting = -1;
	}
	public override void Cancel() {
		if(!isActive) { return; }
		Executor.Cancel();
		base.Cancel();
	}
}