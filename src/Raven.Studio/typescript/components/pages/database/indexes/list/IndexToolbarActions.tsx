﻿import React, { useCallback, useState } from "react";
import { useAppUrls } from "hooks/useAppUrls";
import classNames from "classnames";
import { withPreventDefault } from "components/utils/common";
import IndexLockMode = Raven.Client.Documents.Indexes.IndexLockMode;
import { Button, DropdownItem, DropdownMenu, DropdownToggle, Spinner, UncontrolledDropdown } from "reactstrap";
import { Icon } from "components/common/Icon";

interface IndexToolbarActionProps {
    selectedIndexes: string[];
    deleteSelectedIndexes: () => Promise<void>;
    enableSelectedIndexes: () => Promise<void>;
    disableSelectedIndexes: () => Promise<void>;
    pauseSelectedIndexes: () => Promise<void>;
    resumeSelectedIndexes: () => Promise<void>;
    setLockModeSelectedIndexes: (lockMode: IndexLockMode) => Promise<void>;
}

export default function IndexToolbarAction(props: IndexToolbarActionProps) {
    const { forCurrentDatabase: urls } = useAppUrls();
    const newIndexUrl = urls.newIndex();

    const {
        selectedIndexes,
        deleteSelectedIndexes,
        enableSelectedIndexes,
        disableSelectedIndexes,
        pauseSelectedIndexes,
        resumeSelectedIndexes,
        setLockModeSelectedIndexes,
    } = props;

    const unlockSelectedIndexes = useCallback(
        async (e: React.MouseEvent<HTMLElement>) => {
            e.preventDefault();
            await setLockModeSelectedIndexes("Unlock");
        },
        [setLockModeSelectedIndexes]
    );

    const lockSelectedIndexes = useCallback(
        async (e: React.MouseEvent<HTMLElement>) => {
            e.preventDefault();
            await setLockModeSelectedIndexes("LockedIgnore");
        },
        [setLockModeSelectedIndexes]
    );

    const lockErrorSelectedIndexes = useCallback(
        async (e: React.MouseEvent<HTMLElement>) => {
            e.preventDefault();
            await setLockModeSelectedIndexes("LockedError");
        },
        [setLockModeSelectedIndexes]
    );

    const [globalLockChanges] = useState(false);
    // TODO: IDK I just wanted it to compile

    return (
        <div className="indexesToolbar-actions flex-horizontal">
            <div
                className={classNames("btn-group-label margin-right flex-horizontal", {
                    active: selectedIndexes.length > 0,
                })}
                data-label="Selection"
            >
                <Button
                    color="danger"
                    disabled={selectedIndexes.length === 0}
                    onClick={deleteSelectedIndexes}
                    className="margin-right-xxs"
                >
                    <Icon icon="trash" className="me-1"></Icon>
                    <span>Delete</span>
                </Button>
                <UncontrolledDropdown>
                    <DropdownToggle
                        className="margin-right-xxs"
                        title="Set the indexing state for the selected indexes"
                        disabled={selectedIndexes.length === 0}
                        data-bind="enable: $root.globalIndexingStatus() === 'Running' && selectedIndexesName().length && !spinners.globalLockChanges()"
                    >
                        {globalLockChanges && <Spinner size="sm" className="margin-right-xs" />}
                        {!globalLockChanges && <Icon icon="play" className="me-1"></Icon>}
                        <span>Set indexing state...</span>
                    </DropdownToggle>

                    <DropdownMenu>
                        <DropdownItem onClick={withPreventDefault(enableSelectedIndexes)} title="Enable indexing">
                            <Icon icon="play" className="me-1"></Icon> <span>Enable</span>
                        </DropdownItem>
                        <DropdownItem onClick={withPreventDefault(disableSelectedIndexes)} title="Disable indexing">
                            <Icon icon="disabled" color="danger" className="me-1"></Icon> <span>Disable</span>
                        </DropdownItem>
                        <DropdownItem divider />
                        <DropdownItem onClick={withPreventDefault(resumeSelectedIndexes)} title="Resume indexing">
                            <Icon icon="play" className="me-1"></Icon> <span>Resume</span>
                        </DropdownItem>
                        <DropdownItem onClick={withPreventDefault(pauseSelectedIndexes)} title="Pause indexing">
                            <Icon icon="pause" color="warning" className="me-1"></Icon> <span>Pause</span>
                        </DropdownItem>
                    </DropdownMenu>
                </UncontrolledDropdown>

                <UncontrolledDropdown>
                    <DropdownToggle
                        title="Set the lock mode for the selected indexes"
                        disabled={selectedIndexes.length === 0}
                        data-bind="enable: $root.globalIndexingStatus() === 'Running' && selectedIndexesName().length && !spinners.globalLockChanges()"
                    >
                        {globalLockChanges && <Spinner size="sm" className="margin-right-xs" />}
                        {!globalLockChanges && <Icon icon="lock" className="me-1"></Icon>}
                        <span>Set lock mode...</span>
                    </DropdownToggle>

                    <DropdownMenu>
                        <DropdownItem onClick={unlockSelectedIndexes} title="Unlock selected indexes">
                            <Icon icon="unlock" className="me-1"></Icon> <span>Unlock</span>
                        </DropdownItem>
                        <DropdownItem onClick={lockSelectedIndexes} title="Lock selected indexes">
                            <Icon icon="lock" className="me-1"></Icon> <span>Lock</span>
                        </DropdownItem>
                        <DropdownItem divider />
                        <DropdownItem onClick={lockErrorSelectedIndexes} title="Lock (Error) selected indexes">
                            <Icon icon="lock-error" className="me-1"></Icon> <span>Lock (Error)</span>
                        </DropdownItem>
                    </DropdownMenu>
                </UncontrolledDropdown>
            </div>

            <Button color="primary" href={newIndexUrl}>
                <Icon icon="plus" className="me-1"></Icon>
                <span>New index</span>
            </Button>
        </div>
    );
}
