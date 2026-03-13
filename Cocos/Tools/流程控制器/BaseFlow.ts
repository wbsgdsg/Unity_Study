/**
 * 基础流程类
 */
export abstract class BaseFlow {
    protected flowName: string = "";
    protected isLoop: boolean = false;
    protected onStartCallback?: (flow: BaseFlow) => void;
    protected onEndCallback?: (flow: BaseFlow) => void;

    constructor(name: string, loop: boolean = false) {
        this.flowName = name;
        this.isLoop = loop;
    }

    public getFlowName(): string {
        return this.flowName;
    }

    public getIsLoop(): boolean {
        return this.isLoop;
    }

    public abstract start(): void;
    public abstract update(dt: number): void;
    public abstract end(): void;
    public abstract reset(): void;

    public onPause(): void {}
    public onResume(): void {}

    public setOnStart(callback: (flow: BaseFlow) => void): BaseFlow {
        this.onStartCallback = callback;
        return this;
    }

    public setOnEnd(callback: (flow: BaseFlow) => void): BaseFlow {
        this.onEndCallback = callback;
        return this;
    }

    protected triggerStart(): void {
        if (this.onStartCallback) this.onStartCallback(this);
    }

    protected triggerEnd(): void {
        if (this.onEndCallback) this.onEndCallback(this);
    }
}