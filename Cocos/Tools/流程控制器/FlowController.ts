
import { BaseFlow } from "./BaseFlow";

const {ccclass, property} = cc._decorator;

/**
 * 流程控制器
 */
@ccclass("FlowController")
export class FlowController  extends cc.Component{
    public static instance: FlowController = null;
    private flowMap: Map<string, BaseFlow> = new Map();
    private flowList: BaseFlow[] = [];
    private currentIndex: number = -1;
    private currentFlow: BaseFlow = null;
    private autoNext: boolean = false;
    private isRunning: boolean = false;
    
    // 回调
    private onFlowEndCallback: (flow: BaseFlow) => void = null;
    private onAllCompleteCallback: () => void = null;
  

    /**
     * 注册流程
     */
    public registerFlow(flow: BaseFlow): FlowController {
        if (!flow) return this;
        if (this.flowMap.has(flow.getFlowName())) {
            const existIndex = this.flowList.findIndex((item) => item.getFlowName() === flow.getFlowName());
            if (existIndex !== -1) {
                this.flowList[existIndex] = flow;
            }
        } else {
            this.flowList.push(flow);
        }
        this.flowMap.set(flow.getFlowName(), flow);
        return this;
    }

    /**
     * 开始流程
     */
    public start(index: number = 0): void {
        if (this.isRunning || this.flowList.length === 0) return;
        this.isRunning = true;
        this.currentIndex = -1;
        this.currentFlow = null;
        this.switchToIndex(index);
    }

    /**
     * 下一个流程
     */
    public next(): boolean {
        if (!this.isRunning) return false;
        return this.switchToIndex(this.currentIndex + 1);
    }

    /**
     * 上一个流程
     */
    public previous(): boolean {
        if (!this.isRunning || this.currentIndex <= 0) return false;
        return this.switchToIndex(this.currentIndex - 1);
    }

    /**
     * 切换到指定索引
     */
    public switchToIndex(index: number): boolean {
        if (index < 0 || index >= this.flowList.length) {
            this.onAllComplete();
            return false;
        }

        const targetFlow = this.flowList[index];
        
        // 结束当前流程
        if (this.currentFlow) {
            this.currentFlow.end();
        }

        // 开始新流程
        this.currentFlow = targetFlow;
        this.currentIndex = index;
        this.currentFlow.start();

        return true;
    }

    /**
     * 切换到指定名称
     */
    public switchToName(name: string): boolean {
        const flow = this.flowMap.get(name);
        if (!flow) return false;
        
        const index = this.flowList.indexOf(flow);
        return this.switchToIndex(index);
    }

    /**
     * 结束当前流程并跳转到指定流程
     */
    public endCurrentAndJumpTo(name: string): boolean {
        if (!this.isRunning) return false;

        const targetIndex = this.flowList.findIndex((flow) => flow.getFlowName() === name);
        if (targetIndex === -1) return false;

        if (this.currentFlow) {
            this.currentFlow.end();
            this.currentFlow.reset();
        }

        this.currentFlow = null;
        this.currentIndex = -1;
        return this.switchToIndex(targetIndex);
    }

    /**
     * 结束当前流程并跳转到下一个流程
     */
    public endCurrentAndNext(): boolean {
        if (!this.isRunning) return false;

        if (this.currentFlow) {
            this.currentFlow.end();
            this.currentFlow.reset();
        }

        this.currentFlow = null;
        return this.switchToIndex(this.currentIndex + 1);
    }

    /**
     * 流程结束回调（由流程调用）
     */
    public onFlowEnd(flow: BaseFlow): void {
        if (flow !== this.currentFlow) return;

        if (this.onFlowEndCallback) {
            this.onFlowEndCallback(flow);
        }

        if (flow.getIsLoop()) {
            flow.reset();
            this.switchToIndex(this.currentIndex);
        } else if (this.autoNext) {
            flow.reset();
            this.next();
        }
    }

    /**
     * 所有流程完成
     */
    private onAllComplete(): void {
        this.isRunning = false;
        if (this.onAllCompleteCallback) {
            this.onAllCompleteCallback();
        }
    }

    /**
     * 获取当前流程
     */
    public getCurrentFlow(): BaseFlow | null {
        return this.currentFlow;
    }

    /**
     * 获取当前流程名称
     */
    public getCurrentFlowName(): string {
        return this.currentFlow ? this.currentFlow.getFlowName() : "none";
    }

    /**
     * 获取当前索引
     */
    public getCurrentIndex(): number {
        return this.currentIndex;
    }

    /**
     * 设置自动下一个
     */
    public setAutoNext(auto: boolean): FlowController {
        this.autoNext = auto;
        return this;
    }

    /**
     * 组件加载时调用
     */
    public onLoad(): void {
       FlowController.instance = this;
        this.flowMap = new Map();
        this.flowList = [];
        this.currentIndex = -1;
        this.currentFlow = null;
        this.autoNext = false;
        this.isRunning = false;
        this.onFlowEndCallback = null;
        this.onAllCompleteCallback = null;
    }

    /**
     * 更新
     */
    public update(dt: number): void {
        if (this.isRunning && this.currentFlow) {
            this.currentFlow.update(dt);
        }
    }
    /**
     * 暂停
     */
    public pause(): void {
        if (this.currentFlow) this.currentFlow.onPause();
    }

    /**
     * 恢复
     */
    public resume(): void {
        if (this.currentFlow) this.currentFlow.onResume();
    }

    /**
     * 停止
     */
    public stop(): void {
        if (this.currentFlow) {
            this.currentFlow.end();
            this.currentFlow.reset();
        }
        this.isRunning = false;
        this.currentFlow = null;
        this.currentIndex = -1;
    }

    /**
     * 重置
     */
    public reset(): void {
        this.stop();
        this.flowMap.clear();
        this.flowList = [];
    }
}