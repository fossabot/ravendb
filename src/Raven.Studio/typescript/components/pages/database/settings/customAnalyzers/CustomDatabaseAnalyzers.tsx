﻿import database from "models/resources/database";
import React from "react";
import { Button, Col, Row, UncontrolledTooltip } from "reactstrap";
import { AboutViewAnchored, AboutViewHeading, AccordionItemWrapper } from "components/common/AboutView";
import { Icon } from "components/common/Icon";
import { HrHeader } from "components/common/HrHeader";
import { EmptySet } from "components/common/EmptySet";
import {
    RichPanel,
    RichPanelActions,
    RichPanelHeader,
    RichPanelInfo,
    RichPanelName,
} from "components/common/RichPanel";
import { todo } from "common/developmentHelper";
import AccordionCommunityLicenseLimited from "components/common/AccordionCommunityLicenseLimited";

interface CustomAnalyzersPageProps {
    db: database;
}

interface CustomAnalyzer {
    name: string;
    overridesServerConfig?: boolean;
}

const databaseCustomAnalyzers: CustomAnalyzer[] = [{ name: "customAnalyzer", overridesServerConfig: true }];
const serverCustomAnalyzers: CustomAnalyzer[] = [
    { name: "serverCustomAnalyzer" },
    { name: "dingdong" },
    { name: "dingdong" },
];

const databaseCustomAnalyzersCount = databaseCustomAnalyzers.length;
const serverCustomAnalyzersCount = serverCustomAnalyzers.length;

todo("Feature", "Damian", "Add logic");
todo("Feature", "Damian", "Connect the view to studio");
todo("Feature", "Damian", "Add conditional rendering for Community license");
todo("Feature", "Damian", "Add unique ids for HrHeader popovers");

export default function CustomDatabaseAnalyzers(props: CustomAnalyzersPageProps) {
    const limit = 1; // TODO get from license selector

    return (
        <div className="content-margin">
            <Col xxl={12}>
                <Row className="gy-sm">
                    <Col>
                        <AboutViewHeading title="Custom analyzers" icon="custom-analyzers" />
                        <Button color="primary" className="mb-3" disabled={databaseCustomAnalyzersCount === 1}>
                            <Icon icon="plus" />
                            Add a database custom analyzer
                        </Button>
                        <HrHeader
                            count={databaseCustomAnalyzersCount}
                            limit={limit}
                            right={
                                <a href="https://ravendb.net/l/LGUJH8/6.0" target="_blank">
                                    <Icon icon="link" />
                                    Analyzers tutorial
                                </a>
                            }
                        >
                            Database custom analyzer
                        </HrHeader>
                        {databaseCustomAnalyzersCount > 0 ? (
                            <div className="mb-5">
                                {databaseCustomAnalyzers.map((customAnalyzer, index) => (
                                    <RichPanel key={index} className="mt-3">
                                        <RichPanelHeader>
                                            <RichPanelInfo>
                                                <RichPanelName>{customAnalyzer.name}</RichPanelName>
                                            </RichPanelInfo>
                                            <RichPanelActions>
                                                {customAnalyzer.overridesServerConfig && (
                                                    <div className="d-flex">
                                                        <Icon icon="info" color="info" id="overridesServer" />
                                                        <UncontrolledTooltip target="overridesServer" placeholder="top">
                                                            Overrides server-wide configuration
                                                        </UncontrolledTooltip>
                                                    </div>
                                                )}
                                                <Button>
                                                    <Icon icon="edit" margin="m-0" />
                                                </Button>
                                                <Button color="danger">
                                                    <Icon icon="trash" margin="m-0" />
                                                </Button>
                                            </RichPanelActions>
                                        </RichPanelHeader>
                                    </RichPanel>
                                ))}
                            </div>
                        ) : (
                            <EmptySet className="mb-5">No database custom analyzers have been defined</EmptySet>
                        )}
                        <HrHeader
                            count={serverCustomAnalyzersCount}
                            limit={5}
                            right={
                                <a href="#" target="_blank">
                                    <Icon icon="link" />
                                    Server-wide custom analyzers
                                </a>
                            }
                        >
                            Server-wide custom analyzers
                        </HrHeader>
                        {serverCustomAnalyzersCount > 0 ? (
                            <div>
                                {serverCustomAnalyzers.map((customAnalyzer, index) => (
                                    <RichPanel key={index} className="mt-3">
                                        <RichPanelHeader>
                                            <RichPanelInfo>
                                                <RichPanelName>{customAnalyzer.name}</RichPanelName>
                                            </RichPanelInfo>
                                            <RichPanelActions>
                                                <Button>
                                                    <Icon icon="edit" margin="m-0" />
                                                </Button>
                                                <Button color="danger">
                                                    <Icon icon="trash" margin="m-0" />
                                                </Button>
                                            </RichPanelActions>
                                        </RichPanelHeader>
                                    </RichPanel>
                                ))}
                            </div>
                        ) : (
                            <EmptySet>No server-wide custom analyzers have been defined</EmptySet>
                        )}
                    </Col>
                    <Col sm={12} lg={4}>
                        <AboutViewAnchored>
                            <AccordionItemWrapper
                                targetId="1"
                                icon="about"
                                color="info"
                                description="Get additional info on what this feature can offer you"
                                heading="About this view"
                            >
                                Umm
                            </AccordionItemWrapper>
                            {databaseCustomAnalyzersCount === 1 && (
                                <AccordionCommunityLicenseLimited
                                    targetId="licensing"
                                    featureName="Custom Analyzers"
                                    featureIcon="custom-analyzers"
                                    description={`You've reached the limit of ${limit} database custom analyzer for Community license. Upgrade to a paid plan and get unlimited availability.`}
                                />
                            )}
                        </AboutViewAnchored>
                    </Col>
                </Row>
            </Col>
        </div>
    );
}