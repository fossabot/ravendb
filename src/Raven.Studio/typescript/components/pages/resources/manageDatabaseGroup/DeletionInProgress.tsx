﻿import { FlexGrow } from "components/common/FlexGrow";
import { RichPanel, RichPanelHeader, RichPanelName, RichPanelStatus } from "components/common/RichPanel";
import React from "react";
import { Spinner } from "reactstrap";

export function DeletionInProgress(props: { nodeTag: string }) {
    const { nodeTag } = props;
    return (
        <RichPanel className="flex-row">
            <RichPanelStatus color="danger">deleting</RichPanelStatus>
            <RichPanelHeader className="flex-grow-1">
                <RichPanelName>Node: {nodeTag}</RichPanelName>
                <FlexGrow />
                <div className="pulse text-progress" title="Deletion in progress">
                    <Spinner size="sm" className="me-1" /> Deletion in progress
                </div>
            </RichPanelHeader>
        </RichPanel>
    );
}