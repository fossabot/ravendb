﻿import database from "models/resources/database";
import React from "react";
import { Button, Col, Row } from "reactstrap";
import {
    AboutViewAnchored,
    AboutViewHeading,
    AccordionItemLicensing,
    AccordionItemWrapper,
} from "components/common/AboutView";
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

interface ServerWideSortersPageProps {
    db: database;
}

interface ServerWideSorter {
    name: string;
}
const serverWideSorters: ServerWideSorter[] = [
    { name: "serverCustomSorter" },
    { name: "dingdong" },
    { name: "dingdong" },
    { name: "dingdong" },
];
const serverWideSortersCount = serverWideSorters.length;

todo("Feature", "Damian", "Add logic");
todo("Feature", "Damian", "Connect the view to studio");
todo("Feature", "Damian", "Add conditional rendering for Community license");
todo("Feature", "Damian", "Add unique ids for HrHeader popovers");

export default function ServerWideSorters(props: ServerWideSortersPageProps) {
    return (
        <div className="content-margin">
            <Col xxl={12}>
                <Row className="gy-sm">
                    <Col>
                        <AboutViewHeading title="Server-Wide Sorters" icon="server-wide-custom-sorters" />
                        <Button color="primary" className="mb-3" disabled={serverWideSortersCount === 1}>
                            <Icon icon="plus" />
                            Add a server-wide custom sorter
                        </Button>
                        <HrHeader
                            count={serverWideSortersCount}
                            limit={5}
                            right={
                                <a href="https://ravendb.net/l/LGUJH8/6.0.0-custom-60" target="_blank">
                                    <Icon icon="link" />
                                    Sorters tutorial
                                </a>
                            }
                        >
                            Server-wide custom sorters
                        </HrHeader>
                        {serverWideSortersCount > 0 ? (
                            <div>
                                {serverWideSorters.map((serverWideSorter, index) => (
                                    <RichPanel key={index} className="mt-3">
                                        <RichPanelHeader>
                                            <RichPanelInfo>
                                                <RichPanelName>{serverWideSorter.name}</RichPanelName>
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
                            <EmptySet>No server-wide custom sorters have been defined</EmptySet>
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
                            {serverWideSortersCount === 5 && (
                                <AccordionItemWrapper
                                    targetId="licensing"
                                    icon="license"
                                    color="warning"
                                    description="See available upgrade options"
                                    heading="Licensing"
                                    pill
                                    pillText="Upgrade available"
                                    pillIcon="upgrade-arrow"
                                >
                                    <AccordionItemLicensing
                                        description="You've reached the limit of 1 database custom sorter for Community license. Upgrade to a paid plan and get unlimited availability."
                                        featureName="Custom Sorters"
                                        featureIcon="custom-sorters"
                                        checkedLicenses={["Community", "Professional", "Enterprise"]}
                                        isCommunityLimited
                                    >
                                        <p className="lead fs-4">Get your license expanded</p>
                                        <div className="mb-3">
                                            <Button color="primary" className="rounded-pill">
                                                <Icon icon="notifications" />
                                                Contact us
                                            </Button>
                                        </div>
                                        <small>
                                            <a href="https://ravendb.net/buy" target="_blank" className="text-muted">
                                                See pricing plans
                                            </a>
                                        </small>
                                    </AccordionItemLicensing>
                                </AccordionItemWrapper>
                            )}
                        </AboutViewAnchored>
                    </Col>
                </Row>
            </Col>
        </div>
    );
}